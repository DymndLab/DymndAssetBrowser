using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.Core.Parsing;

public static class AssetParserFactory
{
    public static IAssetFilenameParser Create(string profile) =>
        profile.Equals(LibraryParserProfiles.Fa, StringComparison.OrdinalIgnoreCase)
            ? new FaAssetFilenameParser()
            : new GenericAssetFilenameParser();
}
