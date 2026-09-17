using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DymndAssetBrowser.App.ViewModels;

namespace DymndAssetBrowser.App;

public partial class PlannerFilterCard : UserControl
{
    private Window? _owner;
    private bool _syncing;
    private PlannerFilterViewModel? Vm => DataContext as PlannerFilterViewModel;
    public PlannerFilterCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_owner is not null) return;
            _owner = Window.GetWindow(this);
            if (_owner is null) return;
            _owner.PreviewMouseDown += Owner_MouseDown; _owner.Deactivated += Deactivated; _owner.PreviewKeyDown += KeyDownPopup;
        };
        Unloaded += (_, _) =>
        {
            Close();
            if (_owner is null) return;
            _owner.PreviewMouseDown -= Owner_MouseDown; _owner.Deactivated -= Deactivated; _owner.PreviewKeyDown -= KeyDownPopup; _owner = null;
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Close(); };
        MaterialPopup.Child.PreviewKeyDown += KeyDownPopup; BiomePopup.Child.PreviewKeyDown += KeyDownPopup;
    }
    private void Close() { MaterialPopup.IsOpen = false; BiomePopup.IsOpen = false; }
    private void Deactivated(object? sender, EventArgs e) => Close();
    private void KeyDownPopup(object sender, KeyEventArgs e) { if (e.Key == Key.Escape && (MaterialPopup.IsOpen || BiomePopup.IsOpen)) { Close(); e.Handled = true; } }
    private void Owner_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!Within(e.OriginalSource as DependencyObject, MaterialButton) && !Within(e.OriginalSource as DependencyObject, MaterialPopup.Child)) MaterialPopup.IsOpen = false;
        if (!Within(e.OriginalSource as DependencyObject, BiomeButton) && !Within(e.OriginalSource as DependencyObject, BiomePopup.Child)) BiomePopup.IsOpen = false;
    }
    private static bool Within(DependencyObject? child, DependencyObject root)
    {
        while (child is not null) { if (ReferenceEquals(child, root)) return true; child = child is Visual ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child); }
        return false;
    }
    private void Biome_Click(object sender, RoutedEventArgs e) { MaterialPopup.IsOpen = false; BiomePopup.IsOpen = Infrastructure.PopupOwnerGuard.CanOpen(_owner) && !BiomePopup.IsOpen; }
    private void Materials_Click(object sender, RoutedEventArgs e) { BiomePopup.IsOpen = false; MaterialPopup.IsOpen = Infrastructure.PopupOwnerGuard.CanOpen(_owner) && !MaterialPopup.IsOpen; }
    private void Biomes_Opened(object sender, EventArgs e)
    {
        _syncing = true; BiomeList.UnselectAll();
        if (Vm is { } vm) foreach (var biome in vm.SelectedBiomes) BiomeList.SelectedItems.Add(biome);
        _syncing = false;
    }
    private void Biomes_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || e.AddedItems.Count == 0 && e.RemovedItems.Cast<string>().Any(v => !BiomeList.Items.Contains(v))) return;
        Vm?.SetBiomes(BiomeList.SelectedItems.Cast<string>());
    }
    private void AllBiomes_Click(object sender, RoutedEventArgs e) { _syncing = true; BiomeList.UnselectAll(); _syncing = false; Vm?.SetBiomes([]); }
    private void ClearMaterials_Click(object sender, RoutedEventArgs e) => Vm?.ClearMaterials();
    private void Reset_Click(object sender, RoutedEventArgs e) => Vm?.Reset();
    private void Unpin_Click(object sender, RoutedEventArgs e) => Vm?.ClearPinnedSelection();
}
