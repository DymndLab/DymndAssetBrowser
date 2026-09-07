using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Persistence;
using Microsoft.Win32;

namespace DymndBuilder.V1;

public partial class MainWindow : Window
{
    private const int PixelsPerSquare = 200;
    private static readonly string IndexPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DymndAssetBrowser", "asset-index.db");
    private readonly List<GuideShape> _shapes = [];
    private AssetCatalogIndex _index = AssetCatalogIndex.Empty;
    private WallConstructionCatalog _wallCatalog = WallConstructionCatalog.Empty;
    private IReadOnlyList<AssetRecord> _resolvedWallAssets = [];
    private RenderTargetBitmap? _rendered;
    private GuideShape? _draft;
    private Point _drawStart;
    private bool _drawing;
    private bool _updatingFilters;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartHorizontal;
    private double _panStartVertical;
    private double _zoom = 1;
    private bool _fitQueued;
    private bool _fittingCanvas;
    private GuideLayout? _currentLayout;
    private BitmapSource? _currentRibbon;
    private WallPrefabSet? _currentPrefabs;
    private IReadOnlyList<string> _layerOrder = [];
    private IReadOnlyList<WallPrefabPlacement> _prefabPlacements = [];
    private bool _rightGestureActive;
    private Point _rightDownCanvas;

    public MainWindow()
    {
        InitializeComponent();
        ToolBox.ItemsSource = new[] { "Line", "Rectangle", "Circle" }; ToolBox.SelectedIndex = 0;
        SnapBox.ItemsSource = new[] { "1", "1/2", "1/4", "Off" }; SnapBox.SelectedIndex = 1;
        Loaded += async (_, _) => { await LoadLibraryAsync(); QueueFitCanvas(); };
        ResizeCanvas();
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var assets = await new AssetIndexStore(IndexPath).LoadAllAsync();
            _index = new AssetCatalogIndex(assets);
            _wallCatalog = WallConstructionCatalog.Build(assets);
            _updatingFilters = true;
            SetItems(WallGroupBox, ["Building"], "Building"); SetItems(WallSubGroupBox, ["Walls"], "Walls");
            SetItems(WallMaterialBox, _wallCatalog.Values(new(), WallSetFacet.WallSystem, LibrarySourceIds.ForgottenAdventures), "All");
            SetItems(WallStyleBox, ["All"], "All"); SetItems(WallThemeBox, ["All"], "All"); SetItems(WallFamilyBox, ["All"], "All"); SetItems(WallVariantBox, ["All"], "All");
            _updatingFilters = false;
            RefreshFiltersAndWallSet();
            StatusText.Text = $"Loaded {assets.Count:N0} indexed assets. Choose a wall set, then draw guides.";
        }
        catch (Exception ex) { ShowError("Could not load the Dymnd asset index: " + ex.Message); }
    }

    private WallSetSelection CurrentSelection() => new()
    {
        WallSystem = Selected(WallMaterialBox), PrimaryAppearance = Selected(WallStyleBox),
        Theme = Selected(WallThemeBox), Profile = Selected(WallFamilyBox), Variant = Selected(WallVariantBox)
    };

    private void WallFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFilters && IsLoaded) RefreshFiltersAndWallSet();
    }

    private void RefreshFiltersAndWallSet()
    {
        if (_index.Count == 0) return;
        var selection = CurrentSelection();
        _updatingFilters = true;
        RefreshFacet(WallMaterialBox, WallSetFacet.WallSystem, selection); RefreshFacet(WallStyleBox, WallSetFacet.PrimaryAppearance, selection);
        RefreshFacet(WallThemeBox, WallSetFacet.Theme, selection); RefreshFacet(WallFamilyBox, WallSetFacet.Profile, selection);
        RefreshFacet(WallVariantBox, WallSetFacet.Variant, selection);
        _updatingFilters = false;

        selection = CurrentSelection();
        var palette = BuildModeService.Populate(_index, _wallCatalog, new BuildRecipe { WallSet = selection },
            new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures);
        _resolvedWallAssets = [];
        if (palette.WallRibbon.Status != WallRibbonResolutionStatus.Resolved || palette.WallRibbon.Mapping is not { } mapping)
        {
            WallSetStatus.Text = $"{palette.Walls.Count:N0} matching wall assets · narrow filters until one ribbon resolves";
            return;
        }

        _resolvedWallAssets = palette.Walls.Where(asset =>
            FaWallRibbonCatalog.TryResolve([asset], asset.FamilyKey)?.CspRibbonName.Equals(mapping.CspRibbonName, StringComparison.OrdinalIgnoreCase) == true).ToList();
        RibbonPathBox.Text = FindAsset(asset => asset.FileName.Contains("Straight_Path", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(asset.FileName, "_Path\\.png$", RegexOptions.IgnoreCase))?.FilePath ?? RibbonPathBox.Text;
        CornerPathBox.Text = FindAsset(asset => asset.PartType.Equals("Corner", StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(asset.FileName, "_Corner_(?:In_)?A(?:1)?_", RegexOptions.IgnoreCase))?.FilePath
            ?? FindAsset(asset => asset.PartType.Equals("Corner", StringComparison.OrdinalIgnoreCase))?.FilePath ?? CornerPathBox.Text;
        SutPathBox.Text = mapping.CspRibbonName;
        WallSetStatus.Text = $"{mapping.CspToolGroup} › {mapping.CspRibbonName} · {_resolvedWallAssets.Count:N0} pieces";
    }

    private AssetRecord? FindAsset(Func<AssetRecord, bool> predicate) => _resolvedWallAssets.FirstOrDefault(predicate);
    private void RefreshFacet(ComboBox box, WallSetFacet facet, WallSetSelection selection)
    {
        var old = Selected(box);
        SetItems(box, _wallCatalog.Values(selection, facet, LibrarySourceIds.ForgottenAdventures), old);
    }
    private static void SetItems(ComboBox box, IEnumerable<string> values, string selected)
    {
        var list = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); box.ItemsSource = list;
        box.SelectedItem = list.FirstOrDefault(value => value.Equals(selected, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
    }
    private static string Selected(ComboBox box, string fallback = "All") => box.SelectedItem as string ?? fallback;
    private void ResetWallFilters_Click(object sender, RoutedEventArgs e)
    {
        _updatingFilters = true;
        foreach (var box in new[] { WallMaterialBox, WallStyleBox, WallThemeBox, WallFamilyBox, WallVariantBox }) box.SelectedItem = "All";
        _updatingFilters = false; RefreshFiltersAndWallSet();
    }

    private void ResizeCanvas_Click(object sender, RoutedEventArgs e) => ResizeCanvas();
    private void ResizeCanvas()
    {
        try
        {
            var width = PositiveInt(CanvasWidthBox, "Canvas width"); var height = PositiveInt(CanvasHeightBox, "Canvas height");
            var pixelWidth = width * PixelsPerSquare; var pixelHeight = height * PixelsPerSquare;
            PreviewSurface.Width = InteractionCanvas.Width = GridOverlay.Width = GuideOverlay.Width = pixelWidth;
            PreviewSurface.Height = InteractionCanvas.Height = GridOverlay.Height = GuideOverlay.Height = pixelHeight;
            InvalidateRendered(); _shapes.Clear(); DrawGrid(pixelWidth, pixelHeight); DrawGuides();
            StatusText.Text = $"Canvas resized to {width} × {height} squares ({pixelWidth:N0} × {pixelHeight:N0} export pixels).";
            if (AutoFitBox?.IsChecked == true) QueueFitCanvas();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void InteractionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _drawing = true; _drawStart = Snap(e.GetPosition(InteractionCanvas)); _draft = new GuideShape(SelectedTool(), _drawStart, _drawStart);
        InteractionCanvas.CaptureMouse(); DrawGuides(); e.Handled = true;
    }
    private void InteractionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || e.LeftButton != MouseButtonState.Pressed) return;
        var end = Snap(e.GetPosition(InteractionCanvas));
        if (SelectedTool() == DrawingTool.Line && AngleLockBox.IsChecked == true) end = Lock45(_drawStart, end);
        _draft = new GuideShape(SelectedTool(), _drawStart, end); DrawGuides();
    }
    private void InteractionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing) return;
        _drawing = false; InteractionCanvas.ReleaseMouseCapture();
        if (_draft is { } shape && (shape.End - shape.Start).Length >= 2) _shapes.Add(shape);
        _draft = null; InvalidateRendered(); DrawGuides();
        StatusText.Text = $"{_shapes.Count} guide shape(s). Click Build Walls when the layout is ready."; e.Handled = true;
    }
    private DrawingTool SelectedTool() => ToolBox.SelectedIndex switch { 1 => DrawingTool.Rectangle, 2 => DrawingTool.Circle, _ => DrawingTool.Line };
    private Point Snap(Point point)
    {
        var fraction = SnapBox.SelectedIndex switch { 0 => 1d, 1 => .5, 2 => .25, _ => 0d };
        if (fraction > 0) { var step = PixelsPerSquare * fraction; point = new Point(Math.Round(point.X / step) * step, Math.Round(point.Y / step) * step); }
        return new Point(Math.Clamp(point.X, 0, InteractionCanvas.Width), Math.Clamp(point.Y, 0, InteractionCanvas.Height));
    }
    private Point Lock45(Point start, Point end)
    {
        var delta = end - start; if (delta.Length < 1) return end;
        var angle = Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4d)) * Math.PI / 4d;
        return Snap(new Point(start.X + Math.Cos(angle) * delta.Length, start.Y + Math.Sin(angle) * delta.Length));
    }

    private void DrawGuides()
    {
        GuideOverlay.Children.Clear();
        foreach (var shape in _shapes.Concat(_draft is null ? [] : [_draft]))
        {
            var points = shape.Points(); var polyline = new System.Windows.Shapes.Polyline { Stroke = new SolidColorBrush(Color.FromArgb(220, 210, 155, 71)), StrokeThickness = 3 };
            foreach (var point in points) polyline.Points.Add(point);
            if (shape.Tool is DrawingTool.Rectangle or DrawingTool.Circle) polyline.Points.Add(points[0]);
            GuideOverlay.Children.Add(polyline);
        }
    }

    private void Build_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentRibbon = WallRenderer.LoadImage(RequiredFile(RibbonPathBox.Text, "ribbon PNG"));
            _currentPrefabs = LoadPrefabs();
            _currentLayout = new GuideLayout(_shapes.ToList(), PositiveInt(CanvasWidthBox, "Canvas width"), PositiveInt(CanvasHeightBox, "Canvas height"), PixelsPerSquare, PositiveDouble(FadeBox, "Seam fade"));
            _layerOrder = [];
            RenderCurrentLayers();
            StatusText.Text = $"Built {_shapes.Count} guide shape(s) at native {PixelsPerSquare}px grid resolution. Guides and grid are screen-only.";
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private WallPrefabSet LoadPrefabs()
    {
        var found = new Dictionary<WallPrefabKind, BitmapSource>();
        Add(WallPrefabKind.Corner, asset => asset.PartType.Equals("Corner", StringComparison.OrdinalIgnoreCase) && asset.FileName.Contains("_Corner_A", StringComparison.OrdinalIgnoreCase), CornerPathBox.Text);
        Add(WallPrefabKind.End, asset => asset.PartType.Equals("Ends", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(asset.FileName, "_(?:Ending|End)_A", RegexOptions.IgnoreCase));
        Add(WallPrefabKind.Diagonal, asset => asset.PartType.Equals("Diagonal", StringComparison.OrdinalIgnoreCase) || asset.FileName.Contains("DIAG", StringComparison.OrdinalIgnoreCase));
        Add(WallPrefabKind.TJoint, asset => asset.FileName.Contains("_Joint_B", StringComparison.OrdinalIgnoreCase));
        Add(WallPrefabKind.CrossJoint, asset => asset.FileName.Contains("_Joint_A", StringComparison.OrdinalIgnoreCase));
        return new WallPrefabSet(found);
        void Add(WallPrefabKind kind, Func<AssetRecord, bool> match, string? fallback = null)
        {
            var path = FindAsset(match)?.FilePath ?? fallback;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) found[kind] = WallRenderer.LoadImage(path);
        }
    }

    private void RenderCurrentLayers()
    {
        if (_currentLayout is null || _currentRibbon is null || _currentPrefabs is null) return;
        var result = WallRenderer.RenderWithLayers(_currentLayout, _currentRibbon, _currentPrefabs,
            _layerOrder.Count == 0 ? null : _layerOrder);
        _rendered = result.Bitmap;
        _layerOrder = result.LayerOrder;
        _prefabPlacements = result.Prefabs;
        PreviewImage.Source = _rendered;
    }

    private void InvalidateRendered()
    {
        _rendered = null;
        _currentLayout = null;
        _currentRibbon = null;
        _currentPrefabs = null;
        _layerOrder = [];
        _prefabPlacements = [];
        PreviewImage.Source = null;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) { if (_shapes.Count > 0) _shapes.RemoveAt(_shapes.Count - 1); InvalidateRendered(); DrawGuides(); }
    private void Clear_Click(object sender, RoutedEventArgs e) { _shapes.Clear(); InvalidateRendered(); DrawGuides(); }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_rendered is null) { Build_Click(sender, e); if (_rendered is null) return; }
        var dialog = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "dymnd-building-walls.png", AddExtension = true };
        if (dialog.ShowDialog(this) == true) { WallRenderer.SavePng(_rendered, dialog.FileName); StatusText.Text = "Exported: " + dialog.FileName; }
    }

    private void DrawGrid(double width, double height)
    {
        GridOverlay.Children.Clear(); var stroke = new SolidColorBrush(Color.FromArgb(105, 101, 131, 153)); stroke.Freeze();
        for (var x = 0; x <= width; x += PixelsPerSquare) GridOverlay.Children.Add(new System.Windows.Shapes.Line { X1 = x, X2 = x, Y1 = 0, Y2 = height, Stroke = stroke, StrokeThickness = 1 });
        for (var y = 0; y <= height; y += PixelsPerSquare) GridOverlay.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = width, Y1 = y, Y2 = y, Stroke = stroke, StrokeThickness = 1 });
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ManualZoom(_zoom * 1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ManualZoom(_zoom / 1.25);
    private void ManualZoom(double requestedScale)
    {
        AutoFitBox.IsChecked = false;
        SetZoom(requestedScale, new Point(PreviewScroller.ViewportWidth / 2d, PreviewScroller.ViewportHeight / 2d));
    }
    private void FitCanvas_Click(object sender, RoutedEventArgs e) => FitCanvas();
    private void AutoFitBox_Checked(object sender, RoutedEventArgs e) { if (IsLoaded) QueueFitCanvas(); }
    private void PreviewScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AutoFitBox?.IsChecked == true && !_fittingCanvas) QueueFitCanvas();
    }
    private void QueueFitCanvas()
    {
        if (_fitQueued || !IsLoaded) return;
        _fitQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            _fitQueued = false;
            if (AutoFitBox.IsChecked == true) FitCanvas();
        }));
    }
    private void FitCanvas()
    {
        if (PreviewSurface.Width <= 0 || PreviewSurface.Height <= 0) return;
        _fittingCanvas = true;
        try
        {
            const double padding = 48;
            var viewportWidth = Math.Max(1, PreviewScroller.ActualWidth - padding);
            var viewportHeight = Math.Max(1, PreviewScroller.ActualHeight - padding);
            SetZoom(PreviewZoom.FitScale(PreviewSurface.Width, PreviewSurface.Height, viewportWidth, viewportHeight), null);
            PreviewScroller.ScrollToHorizontalOffset(0);
            PreviewScroller.ScrollToVerticalOffset(0);
        }
        finally { _fittingCanvas = false; }
    }
    private void PreviewScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        AutoFitBox.IsChecked = false;
        var factor = e.Delta > 0 ? 1.15 : 1d / 1.15;
        SetZoom(_zoom * factor, e.GetPosition(PreviewScroller));
        e.Handled = true;
    }
    private void SetZoom(double requestedScale, Point? viewportPivot)
    {
        var next = PreviewZoom.Clamp(requestedScale);
        if (Math.Abs(next - _zoom) < 0.0001) return;
        var old = _zoom;
        var oldHorizontal = PreviewScroller.HorizontalOffset;
        var oldVertical = PreviewScroller.VerticalOffset;
        _zoom = next;
        PreviewScale.ScaleX = PreviewScale.ScaleY = _zoom;
        ZoomText.Text = $"{_zoom:P0}";
        PreviewScroller.UpdateLayout();
        if (viewportPivot is not { } pivot) return;
        const double padding = 24;
        PreviewScroller.ScrollToHorizontalOffset(PreviewZoom.OffsetForPivot(oldHorizontal, pivot.X, padding, old, _zoom));
        PreviewScroller.ScrollToVerticalOffset(PreviewZoom.OffsetForPivot(oldVertical, pivot.Y, padding, old, _zoom));
    }
    private void PreviewScroller_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rightGestureActive = true;
        _isPanning = false;
        _panStart = e.GetPosition(PreviewScroller);
        _rightDownCanvas = e.GetPosition(InteractionCanvas);
        _panStartHorizontal = PreviewScroller.HorizontalOffset;
        _panStartVertical = PreviewScroller.VerticalOffset;
        Mouse.Capture(PreviewScroller);
        e.Handled = true;
    }
    private void PreviewScroller_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_rightGestureActive || e.RightButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(PreviewScroller);
        if (!_isPanning && (point - _panStart).Length < 5) return;
        _isPanning = true;
        PreviewScroller.Cursor = Cursors.SizeAll;
        PreviewScroller.ScrollToHorizontalOffset(_panStartHorizontal - point.X + _panStart.X);
        PreviewScroller.ScrollToVerticalOffset(_panStartVertical - point.Y + _panStart.Y);
        e.Handled = true;
    }
    private void PreviewScroller_RightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_rightGestureActive) return;
        var wasPanning = _isPanning;
        EndRightGesture();
        if (!wasPanning) ShowPrefabLayerMenu(_rightDownCanvas);
        e.Handled = true;
    }
    private void PreviewScroller_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _rightGestureActive = false;
        _isPanning = false;
        PreviewScroller.Cursor = Cursors.Arrow;
    }
    private void EndRightGesture()
    {
        _rightGestureActive = false;
        _isPanning = false;
        PreviewScroller.Cursor = Cursors.Arrow;
        if (Mouse.Captured == PreviewScroller) Mouse.Capture(null);
    }
    private void ShowPrefabLayerMenu(Point canvasPoint)
    {
        var layerPositions = _layerOrder.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        var hit = _prefabPlacements.Where(prefab => prefab.Contains(canvasPoint))
            .OrderByDescending(prefab => layerPositions.GetValueOrDefault(prefab.Id, -1)).FirstOrDefault();
        if (hit is null) return;

        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        Add("Bring to Front", WallLayerMove.BringToFront);
        Add("Bring Forward", WallLayerMove.BringForward);
        menu.Items.Add(new Separator());
        Add("Send Backward", WallLayerMove.SendBackward);
        Add("Send to Back", WallLayerMove.SendToBack);
        menu.IsOpen = true;

        void Add(string header, WallLayerMove move)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => MovePrefabLayer(hit, move);
            menu.Items.Add(item);
        }
    }
    private void MovePrefabLayer(WallPrefabPlacement prefab, WallLayerMove move)
    {
        var changed = WallLayerOrder.Move(_layerOrder, prefab.Id, move);
        if (changed.SequenceEqual(_layerOrder))
        {
            StatusText.Text = $"{prefab.Kind} is already at that layer limit.";
            return;
        }
        _layerOrder = changed;
        RenderCurrentLayers();
        StatusText.Text = $"Moved {prefab.Kind}: {move switch { WallLayerMove.BringToFront => "front", WallLayerMove.BringForward => "forward", WallLayerMove.SendBackward => "backward", _ => "back" }}.";
    }
    private static int PositiveInt(TextBox box, string label) => int.TryParse(box.Text, out var value) && value > 0 ? value : throw new InvalidOperationException(label + " must be a positive whole number.");
    private static double PositiveDouble(TextBox box, string label) => double.TryParse(box.Text, out var value) && value >= 0 ? value : throw new InvalidOperationException(label + " must be zero or greater.");
    private static string RequiredFile(string value, string label) => File.Exists(value.Trim()) ? value.Trim() : throw new FileNotFoundException($"Choose a valid {label}.", value);
    private void ShowError(string message) { StatusText.Text = "Error: " + message; MessageBox.Show(this, message, "Dymnd Builder", MessageBoxButton.OK, MessageBoxImage.Warning); }
}
