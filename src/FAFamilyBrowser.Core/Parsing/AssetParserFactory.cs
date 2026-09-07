using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Parsing;

public static class AssetParserFactory
{
    public static IAssetFilenameParser Create(string profile) =>
        profile.Equals(LibraryParserProfiles.Fa, StringComparison.OrdinalIgnoreCase)
            ? new FaAssetFilenameParser()
            : new GenericAssetFilenameParser();
}
