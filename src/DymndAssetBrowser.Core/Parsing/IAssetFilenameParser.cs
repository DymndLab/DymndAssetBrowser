using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.Core.Parsing;

public interface IAssetFilenameParser
{
    AssetRecord Parse(string sourceRoot, string filePath, IReadOnlySet<string>? styleVocabulary = null);
}
