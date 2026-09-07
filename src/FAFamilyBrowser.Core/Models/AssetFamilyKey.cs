namespace FAFamilyBrowser.Core.Models;

public static class AssetFamilyKey
{
    public static string Create(string group, string subGroup, string material, string style, string theme, string family) =>
        string.Join("|", group, subGroup, material, style, theme, family);

    public static string Create(string group, string subGroup, string material, string style, string theme, string family, string variant) =>
        string.Join("|", group, subGroup, material, style, theme, family, variant);

    // Schema-v1 ribbon key format.
    public static string Create(string domain, string material, string style, string family) =>
        string.Join("|", domain, material, style, family);
}
