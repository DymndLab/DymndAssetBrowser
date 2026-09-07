namespace FAFamilyBrowser.Core.Models;

public static class FaWallRibbonCatalog
{
    public static RibbonMapping? TryResolve(IReadOnlyList<AssetRecord> walls, string mappingKey) =>
        FaStoneWallRibbonCatalog.TryResolve(walls, mappingKey)
        ?? FaMixedWallRibbonCatalog.TryResolve(walls, mappingKey)
        ?? FaAdditionalWallRibbonCatalog.TryResolve(walls, mappingKey)
        ?? FaSpecialtyWallRibbonCatalog.TryResolve(walls, mappingKey);
}
