namespace FAFamilyBrowser.Core.Models;

public static class LibraryParserProfiles
{
    public const string Fa = "FA";
    public const string Generic = "Generic";
}

public static class LibrarySourceIds
{
    public const string ForgottenAdventures = "forgotten-adventures";
}

public sealed record AssetLibrarySource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string RootPath { get; init; }
    public string ParserProfile { get; init; } = LibraryParserProfiles.Generic;
}
