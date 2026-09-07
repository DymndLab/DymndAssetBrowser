namespace FAFamilyBrowser.Core.Models;

public sealed record RibbonMapping
{
    public required string FamilyKey { get; init; }
    public string CspTool { get; init; } = string.Empty;
    public string CspToolGroup { get; init; } = string.Empty;
    public string CspRibbonName { get; init; } = string.Empty;
    public string? SuggestedRibbonName { get; init; }
    public double? SuggestionConfidence { get; init; }
    public bool IsManuallyConfirmed { get; init; } = true;
}
