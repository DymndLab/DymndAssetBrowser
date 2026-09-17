using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DymndAssetBrowser.App.ViewModels;

namespace DymndAssetBrowser.App;

public partial class SourceBrowserControls : UserControl
{
    private Window? _owner;
    public SourceBrowserControls()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_owner is not null) return;
            _owner = Window.GetWindow(this);
            if (_owner is null) return;
            _owner.PreviewMouseDown += Owner_MouseDown;
            _owner.Deactivated += Owner_Deactivated;
            _owner.PreviewKeyDown += Popup_KeyDown;
        };
        Unloaded += (_, _) =>
        {
            ClosePopups();
            if (_owner is null) return;
            _owner.PreviewMouseDown -= Owner_MouseDown;
            _owner.Deactivated -= Owner_Deactivated;
            _owner.PreviewKeyDown -= Popup_KeyDown;
            _owner = null;
        };
        MaterialPopup.Child.PreviewKeyDown += Popup_KeyDown;
        BiomePopup.Child.PreviewKeyDown += Popup_KeyDown;
    }
    private void MaterialButton_Click(object sender, RoutedEventArgs e)
    { BiomePopup.IsOpen = false; MaterialPopup.IsOpen = Infrastructure.PopupOwnerGuard.CanOpen(_owner) && !MaterialPopup.IsOpen; }
    private void BiomeButton_Click(object sender, RoutedEventArgs e)
    { MaterialPopup.IsOpen = false; BiomePopup.IsOpen = Infrastructure.PopupOwnerGuard.CanOpen(_owner) && !BiomePopup.IsOpen; }
    private void ClosePopups() { MaterialPopup.IsOpen = false; BiomePopup.IsOpen = false; }
    private void Owner_Deactivated(object? sender, EventArgs e) => ClosePopups();
    private void Owner_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!Within(e.OriginalSource as DependencyObject, MaterialButton) && !Within(e.OriginalSource as DependencyObject, MaterialPopup.Child)) MaterialPopup.IsOpen = false;
        if (!Within(e.OriginalSource as DependencyObject, BiomeButton) && !Within(e.OriginalSource as DependencyObject, BiomePopup.Child)) BiomePopup.IsOpen = false;
    }
    private static bool Within(DependencyObject? element, DependencyObject root)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, root)) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
    private void Popup_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Escape && (MaterialPopup.IsOpen || BiomePopup.IsOpen)) { ClosePopups(); e.Handled = true; } }
    private MainViewModel? Vm => DataContext as MainViewModel;
    private bool _syncingBiomes;
    private void Biomes_Opened(object sender, EventArgs e)
    {
        _syncingBiomes = true;
        BiomeList.UnselectAll();
        if (Vm is { } vm) foreach (var b in vm.SelectedBiomes) BiomeList.SelectedItems.Add(b);
        _syncingBiomes = false;
    }
    private void Biomes_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingBiomes) return;
        // Ignore removals caused by replacing the option list during a progressive rebuild.
        if (e.AddedItems.Count == 0 && e.RemovedItems.Cast<string>().Any(s => !BiomeList.Items.Contains(s))) return;
        Vm?.SetBiomes(BiomeList.SelectedItems.Cast<string>());
    }
    private void AllBiomes_Click(object sender, RoutedEventArgs e) { BiomeList.UnselectAll(); Vm?.SetBiomes([]); }
    private void ClearMaterials_Click(object sender, RoutedEventArgs e) => Vm?.ClearMaterials();
    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
        SearchBox.CaretIndex = 0;
    }
    private void Reset_Click(object sender, RoutedEventArgs e) { BiomeList.UnselectAll(); Vm?.ResetFilters(); }
    private void SelectAll_Click(object sender, RoutedEventArgs e) => Vm?.SelectAllBrowserResults();
    private void NewAssets_Click(object sender, RoutedEventArgs e) => Vm?.ToggleNewAssets();
    private void EditTags_Click(object sender, RoutedEventArgs e) { if (Vm is { } vm) ShowTagEditor(Window.GetWindow(this), vm); }

    public static void ShowTagEditor(Window owner, MainViewModel vm)
    {
        var dialog = new TagEditorWindow(vm) { Owner = owner };
        dialog.ShowDialog();
    }
}
