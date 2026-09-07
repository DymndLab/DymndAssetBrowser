using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Parsing;

public interface IAssetFilenameParser
{
    AssetRecord Parse(string sourceRoot, string filePath, IReadOnlySet<string>? styleVocabulary = null);
}
