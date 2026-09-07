namespace FAFamilyBrowser.App.ViewModels;

public sealed record PartSectionViewModel(string Name, IReadOnlyList<AssetTileViewModel> Assets);
