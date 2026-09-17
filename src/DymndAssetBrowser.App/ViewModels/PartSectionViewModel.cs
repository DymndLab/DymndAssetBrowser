namespace DymndAssetBrowser.App.ViewModels;

public sealed class PartSectionViewModel(string name, int count, Func<IReadOnlyList<AssetTileViewModel>> createAssets)
{
    private readonly Lazy<IReadOnlyList<AssetTileViewModel>> _assets = new(createAssets);
    public string Name { get; } = name;
    public int Count { get; } = count;
    public bool AreAssetsCreated => _assets.IsValueCreated;
    public IReadOnlyList<AssetTileViewModel> Assets => _assets.Value;
}
