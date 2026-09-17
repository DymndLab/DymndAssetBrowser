using System.Text.Json;
using System.Security.Cryptography;
using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.Core.Persistence;

public sealed class ApplicationStateStore
{
    private readonly SemaphoreSlim _metadataGate = new(1, 1);
    private string? _expectedMetadataHash;
    private bool _snapshotLoaded;
    private bool _saveBlocked;
    public bool HasSettingsBackup => File.Exists(StatePath + ".bak");
    private static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private void EnsureWritable()
    {
        if (_saveBlocked) throw new IOException("Saving is disabled because startup data could not be loaded. Recover the saved settings before making changes.");
    }
    public const string CurrentAppDataFolderName = "DymndAssetBrowser";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public ApplicationStateStore(string? appDataRoot = null)
    {
        var configuredRoot = Environment.GetEnvironmentVariable("DYMND_ASSET_BROWSER_STATE_ROOT");
        if (appDataRoot is not null)
        {
            AppDataRoot = Path.GetFullPath(appDataRoot);
        }
        else if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            AppDataRoot = Path.GetFullPath(configuredRoot);
        }
        else
        {
            AppDataRoot = GetDefaultAppDataRoot();
        }
        StatePath = Path.Combine(AppDataRoot, "settings.json");
        IndexPath = Path.Combine(AppDataRoot, "asset-index.db");
        AssetIndex = new AssetIndexStore(IndexPath);
    }

    public string AppDataRoot { get; }
    public string StatePath { get; }
    public string IndexPath { get; }
    public string CacheRoot => Path.Combine(AppDataRoot, "cache");
    public AssetIndexStore AssetIndex { get; }

    public static string GetDefaultAppDataRoot()
    {
        // Packaged automation hosts can blank USERPROFILE and redirect known folders into
        // their own LocalCache. A user-local installation still exposes its physical path,
        // so prefer the profile prefix before \AppData\Local when it is available.
        var processPath = Environment.ProcessPath ?? string.Empty;
        var appDataMarker = $"{Path.DirectorySeparatorChar}AppData{Path.DirectorySeparatorChar}Local{Path.DirectorySeparatorChar}";
        var markerIndex = processPath.IndexOf(appDataMarker, StringComparison.OrdinalIgnoreCase);
        var userProfile = markerIndex > 0 ? processPath[..markerIndex] : Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var stableLocalAppData = string.IsNullOrWhiteSpace(userProfile)
            ? Environment.GetEnvironmentVariable("LOCALAPPDATA")
            : Path.Combine(userProfile, "AppData", "Local");
        return Path.Combine(
            string.IsNullOrWhiteSpace(stableLocalAppData)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : stableLocalAppData,
            CurrentAppDataFolderName);
    }

    public async Task<ApplicationState> LoadAsync(CancellationToken cancellationToken = default)
    {
        _saveBlocked = true;
        if (!File.Exists(StatePath))
        {
            _expectedMetadataHash = null; _snapshotLoaded = true; _saveBlocked = false;
            return new ApplicationState { CacheRoot = CacheRoot };
        }
        var bytes = await File.ReadAllBytesAsync(StatePath, cancellationToken);
        var settings = ParseSettings(bytes);
        var assets = await AssetIndex.LoadAllAsync(cancellationToken);
        _expectedMetadataHash = Fingerprint(bytes); _snapshotLoaded = true; _saveBlocked = false;
        return settings with { Assets = assets, CacheRoot = CacheRoot };
    }

    public Task SaveAsync(ApplicationState state, CancellationToken cancellationToken = default)
        => SaveSnapshotAsync(state, replaceAssets: true, cancellationToken);

    public Task SaveMetadataAsync(ApplicationState state, CancellationToken cancellationToken = default)
        => SaveSnapshotAsync(state, replaceAssets: false, cancellationToken);

    private async Task SaveSnapshotAsync(ApplicationState state, bool replaceAssets, CancellationToken cancellationToken)
    {
        EnsureWritable();
        await _metadataGate.WaitAsync(cancellationToken);
        try
        {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(CacheRoot);
        await using var writerLock = await AcquireWriterLockAsync(cancellationToken);
        var current = File.Exists(StatePath) ? await File.ReadAllBytesAsync(StatePath, cancellationToken) : null;
        var actualHash = current is null ? null : Fingerprint(current);
        if ((!_snapshotLoaded && current is not null) || actualHash != _expectedMetadataHash)
            throw new IOException("Settings changed in another instance. Close and reopen the browser before saving; the newer settings were not overwritten.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state with { Assets = [], CacheRoot = CacheRoot }, JsonOptions);
        // Reject stale snapshots before touching the index, not just the settings file.
        if (replaceAssets) await AssetIndex.ReplaceAllAsync(state.Assets, cancellationToken);
        if (current is not null) { ParseSettings(current); File.Copy(StatePath, StatePath + ".bak", overwrite: true); }
        await WriteSettingsAtomicallyAsync(bytes, cancellationToken);
        _expectedMetadataHash = Fingerprint(bytes); _snapshotLoaded = true;
        }
        finally { _metadataGate.Release(); }
    }

    public Task ReplaceAssetsAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default)
    { EnsureWritable(); return AssetIndex.ReplaceAllAsync(assets, cancellationToken); }

    public Task UpsertAssetsAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default)
    { EnsureWritable(); return AssetIndex.UpsertAsync(assets, cancellationToken); }

    public Task ReplaceSourceAssetsAsync(string sourceId, IEnumerable<AssetRecord> assets,
        CancellationToken cancellationToken = default)
    { EnsureWritable(); return AssetIndex.ReplaceSourceAsync(sourceId, assets, cancellationToken); }

    public Task DeleteSourceAssetsAsync(string sourceId, CancellationToken cancellationToken = default)
    { EnsureWritable(); return AssetIndex.DeleteSourceAsync(sourceId, cancellationToken); }

    private static ApplicationState ParseSettings(byte[] bytes)
    {
        var state = JsonSerializer.Deserialize<ApplicationState>(bytes, JsonOptions) ?? throw new JsonException("Settings contain no application state.");
        if (state.Libraries is null || state.RibbonMappings is null || state.UserTags is null || state.BuildRecipe is null || state.BrowserFilter is null
            || state.Libraries.Any(l => l is null || l.RootPath is null || l.ParserProfile is null))
            throw new JsonException("Settings contain missing required data.");
        return Normalize(state);
    }

    private async Task<FileStream> AcquireWriterLockAsync(CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(AppDataRoot, "metadata.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTime.UtcNow < until) { await Task.Delay(50, cancellationToken); }
        }
    }

    private async Task WriteSettingsAtomicallyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var temporaryPath = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await stream.WriteAsync(bytes, cancellationToken); stream.Flush(flushToDisk: true); }
            File.Move(temporaryPath, StatePath, overwrite: true);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }

    // Called only after an explicit recovery choice. Keep the damaged file for inspection.
    public async Task RecoverSettingsAsync(bool restoreBackup, CancellationToken cancellationToken = default)
    {
        await _metadataGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(AppDataRoot);
            await using var writerLock = await AcquireWriterLockAsync(cancellationToken);
            var bytes = restoreBackup ? await File.ReadAllBytesAsync(StatePath + ".bak", cancellationToken)
                : JsonSerializer.SerializeToUtf8Bytes(new ApplicationState(), JsonOptions);
            ParseSettings(bytes);
            if (File.Exists(StatePath)) File.Copy(StatePath, StatePath + ".recovery-" + Guid.NewGuid().ToString("N"), overwrite: false);
            await WriteSettingsAtomicallyAsync(bytes, cancellationToken);
            _expectedMetadataHash = Fingerprint(bytes); _snapshotLoaded = true; _saveBlocked = false;
        }
        finally { _metadataGate.Release(); }
    }

    public static string NormalizePath(string path) => AssetIdentity.NormalizePath(path);

    private static ApplicationState Normalize(ApplicationState state)
    {
        var libraries = state.Libraries.Select(library => library with
        {
            RootPath = NormalizePath(library.RootPath),
            Name = string.IsNullOrWhiteSpace(library.Name) ? Path.GetFileName(library.RootPath) : library.Name.Trim(),
            ParserProfile = library.ParserProfile == LibraryParserProfiles.Fa ? LibraryParserProfiles.Fa : LibraryParserProfiles.Generic
        }).ToList();
        var mappings = new Dictionary<string, RibbonMapping>(state.RibbonMappings, StringComparer.OrdinalIgnoreCase);
        return state with
        {
            SchemaVersion = 6,
            Libraries = libraries,
            RibbonMappings = mappings,
            LastGroup = state.LastGroup ?? "All",
            LastSubGroup = state.LastSubGroup ?? "All",
            LastSubtype = state.LastSubtype ?? "All",
            LastTheme = state.LastTheme ?? "All",
            LastFamily = state.LastFamily ?? "All",
            LastVariant = state.LastVariant ?? "All",
            LastSourceId = state.LastSourceId ?? AssetQueries.All
        };
    }
}
