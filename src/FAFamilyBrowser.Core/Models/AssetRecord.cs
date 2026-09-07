namespace FAFamilyBrowser.Core.Models;

public sealed record AssetRecord
{
    public string SourceId { get; init; } = string.Empty;
    public required string SourceRoot { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public string RelativePath { get; init; } = string.Empty;
    public string RawTokens { get; init; } = string.Empty;
    public string Group { get; init; } = "Unspecified";
    public string SubGroup { get; init; } = "Unspecified";
    public string Material { get; init; } = "Unspecified";
    public string Style { get; init; } = "Unspecified";
    public string Theme { get; init; } = "Unspecified";
    public string Family { get; init; } = "Unspecified";
    public string Variant { get; init; } = "Unspecified";
    public string PartType { get; init; } = "Other";
    public string PartVariant { get; init; } = string.Empty;
    public string EncodedSize { get; init; } = string.Empty;
    public double ParseConfidence { get; init; }

    public string FamilyKey => AssetFamilyKey.Create(Group, SubGroup, Material, Style, Theme, Family, Variant);
    public string StableIdentity => AssetIdentity.Create(SourceId, FilePath);
}
