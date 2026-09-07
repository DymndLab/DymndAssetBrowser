using System.Text.Json;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Persistence;

public sealed class ApplicationStateStore
{
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
        if (!File.Exists(StatePath)) return new ApplicationState { CacheRoot = CacheRoot };
        var settings = Normalize(await ReadJsonAsync(StatePath, cancellationToken));
        var assets = await AssetIndex.LoadAllAsync(cancellationToken);
        return settings with { Assets = assets, CacheRoot = CacheRoot };
    }

    public async Task SaveAsync(ApplicationState state, CancellationToken cancellationToken = default)
    {
        await AssetIndex.ReplaceAllAsync(state.Assets, cancellationToken);
        await SaveMetadataAsync(state, cancellationToken);
    }

    public async Task SaveMetadataAsync(ApplicationState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(CacheRoot);
        var temporaryPath = StatePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, state with { Assets = [], CacheRoot = CacheRoot }, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, StatePath, overwrite: true);
    }

    public Task ReplaceAssetsAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default) =>
        AssetIndex.ReplaceAllAsync(assets, cancellationToken);

    public Task UpsertAssetsAsync(IEnumerable<AssetRecord> assets, CancellationToken cancellationToken = default) =>
        AssetIndex.UpsertAsync(assets, cancellationToken);

    public Task ReplaceSourceAssetsAsync(string sourceId, IEnumerable<AssetRecord> assets,
        CancellationToken cancellationToken = default) => AssetIndex.ReplaceSourceAsync(sourceId, assets, cancellationToken);

    public Task DeleteSourceAssetsAsync(string sourceId, CancellationToken cancellationToken = default) =>
        AssetIndex.DeleteSourceAsync(sourceId, cancellationToken);

    private static async Task<ApplicationState> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ApplicationState>(stream, JsonOptions, cancellationToken)
            ?? new ApplicationState();
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
