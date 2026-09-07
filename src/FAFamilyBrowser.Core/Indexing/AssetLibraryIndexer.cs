using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Parsing;

namespace FAFamilyBrowser.Core.Indexing;

public sealed record IndexProgress(int Discovered, int Indexed);

public sealed class AssetLibraryIndexer
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
    public Task<List<AssetRecord>> ScanLibraryAsync(AssetLibrarySource source, IProgress<IndexProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        ScanLibraryCoreAsync(source, AssetParserFactory.Create(source.ParserProfile), progress, cancellationToken);

    public Task<List<AssetRecord>> ScanLibraryAsync(string sourceRoot, IProgress<IndexProgress>? progress = null, CancellationToken cancellationToken = default) =>
        ScanLibraryCoreAsync(new AssetLibrarySource { Id = LibrarySourceIds.ForgottenAdventures, Name = "Forgotten Adventures", RootPath = sourceRoot, ParserProfile = LibraryParserProfiles.Fa },
            new FaAssetFilenameParser(), progress, cancellationToken);

    private static Task<List<AssetRecord>> ScanLibraryCoreAsync(AssetLibrarySource source, IAssetFilenameParser parser,
        IProgress<IndexProgress>? progress, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var sourceRoot = source.RootPath;
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);
        var files = new List<string>();
        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SupportedExtensions.Contains(Path.GetExtension(path))) continue;
            files.Add(path);
            if (files.Count % 1000 == 0) progress?.Report(new IndexProgress(files.Count, 0));
        }
        IReadOnlySet<string>? styles = source.ParserProfile.Equals(LibraryParserProfiles.Fa, StringComparison.OrdinalIgnoreCase)
            ? FaAssetFilenameParser.DiscoverStyleVocabulary(files.Where(path => Path.GetFileName(path).Contains("Wall_", StringComparison.OrdinalIgnoreCase)))
            : null;
        var results = new List<AssetRecord>(files.Count);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = parser.Parse(sourceRoot, path, styles) with { SourceId = source.Id };
            results.Add(parsed);
            if (results.Count % 1000 == 0) progress?.Report(new IndexProgress(files.Count, results.Count));
        }
        progress?.Report(new IndexProgress(files.Count, results.Count));
        return results.OrderBy(a => a.Group, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.SubGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Material, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Style, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Theme, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }, cancellationToken);
}
