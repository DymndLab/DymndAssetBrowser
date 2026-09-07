using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DymndBuilder.V1;

public sealed record RibbonLayout(
    int SourceTopRow,
    int SourceVisibleHeight,
    double AssetScale,
    double CanvasThickness,
    double VisibleThickness,
    double RepeatDistance);

public enum WallPrefabKind { End, Corner, Diagonal, TJoint, CrossJoint }

public sealed record WallPrefabSet(IReadOnlyDictionary<WallPrefabKind, BitmapSource> Images)
{
    public BitmapSource? Get(WallPrefabKind kind) => Images.TryGetValue(kind, out var image)
        ? image
        : Images.TryGetValue(WallPrefabKind.Corner, out var fallback) ? fallback : null;
}

public sealed record WallPrefabPlacement(
    string Id,
    Point Center,
    WallPrefabKind Kind,
    double Rotation,
    double Width,
    double Height)
{
    public bool Contains(Point point)
    {
        var radians = -Rotation * Math.PI / 180d;
        var delta = point - Center;
        var localX = delta.X * Math.Cos(radians) - delta.Y * Math.Sin(radians);
        var localY = delta.X * Math.Sin(radians) + delta.Y * Math.Cos(radians);
        return Math.Abs(localX) <= Width / 2d && Math.Abs(localY) <= Height / 2d;
    }
}

public sealed record WallRenderResult(
    RenderTargetBitmap Bitmap,
    IReadOnlyList<string> LayerOrder,
    IReadOnlyList<WallPrefabPlacement> Prefabs);

public static class WallRenderer
{
    public static BitmapSource LoadImage(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Image file not found.", path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.UriSource = new Uri(Path.GetFullPath(path));
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static RenderTargetBitmap Render(BuildPlan plan, BitmapSource ribbon, BitmapSource corner)
    {
        var cornerScale = plan.PixelsPerSquare / (double)Math.Max(corner.PixelWidth, corner.PixelHeight);
        var ribbonLayout = CalculateRibbonLayout(ribbon, plan.PixelsPerSquare);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            foreach (var segment in plan.Segments())
                DrawRibbon(drawing, ribbon, segment.Start, segment.End, ribbonLayout);
            for (var index = 0; index < plan.Intersections.Count; index++)
                DrawCorner(drawing, corner, plan.Intersections[index], BuilderGeometry.OutgoingRotation(plan, index), cornerScale);
        }

        var target = new RenderTargetBitmap(plan.PixelWidth, plan.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    public static RenderTargetBitmap Render(GuideLayout layout, BitmapSource ribbon, WallPrefabSet prefabs)
        => RenderWithLayers(layout, ribbon, prefabs).Bitmap;

    public static WallRenderResult RenderWithLayers(
        GuideLayout layout,
        BitmapSource ribbon,
        WallPrefabSet prefabs,
        IReadOnlyList<string>? requestedLayerOrder = null)
    {
        if (layout.Shapes.Count == 0) throw new InvalidOperationException("Draw at least one wall guide first.");
        var ribbonLayout = CalculateRibbonLayout(ribbon, layout.PixelsPerSquare);
        var segments = layout.Segments;
        var nodes = BuildNodes(segments);
        var elements = new List<RenderElement>();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var startNode = nodes.GetValueOrDefault(PointKey(segment.Start));
            var endNode = nodes.GetValueOrDefault(PointKey(segment.End));
            elements.Add(new RenderElement($"ribbon:{index}", null, drawing =>
                DrawRibbon(drawing, ribbon, segment.Start, segment.End, ribbonLayout,
                    segment.HasPrefabEnds && startNode?.Kind is not null,
                    segment.HasPrefabEnds && endNode?.Kind is not null,
                    layout.FadePixels)));
        }

        foreach (var pair in nodes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var node = pair.Value;
            if (node.Kind is not { } kind) continue;
            var image = prefabs.Get(kind);
            if (image is null) continue;
            var scale = layout.PixelsPerSquare / (double)Math.Max(image.PixelWidth, image.PixelHeight);
            var placement = new WallPrefabPlacement(
                $"prefab:{pair.Key}", node.Center, kind, node.Rotation,
                image.PixelWidth * scale, image.PixelHeight * scale);
            elements.Add(new RenderElement(placement.Id, placement, drawing =>
                DrawCorner(drawing, image, node.Center, node.Rotation, scale)));
        }

        var byId = elements.ToDictionary(element => element.Id, StringComparer.Ordinal);
        var ordered = new List<RenderElement>();
        if (requestedLayerOrder is not null)
        {
            foreach (var id in requestedLayerOrder)
                if (byId.Remove(id, out var element)) ordered.Add(element);
        }
        ordered.AddRange(elements.Where(element => byId.ContainsKey(element.Id)));
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            foreach (var element in ordered) element.Draw(drawing);

        var target = new RenderTargetBitmap(layout.PixelWidth, layout.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return new WallRenderResult(target, ordered.Select(element => element.Id).ToList(),
            elements.Where(element => element.Prefab is not null).Select(element => element.Prefab!).ToList());
    }

    public static RenderTargetBitmap RenderJointCalibration(
        BitmapSource ribbon,
        BitmapSource corner,
        BitmapSource tJoint,
        int pixelsPerSquare = 200)
    {
        var width = pixelsPerSquare * 8;
        var height = pixelsPerSquare * 4;
        var left = new Point(pixelsPerSquare, pixelsPerSquare);
        var junction = new Point(pixelsPerSquare * 4, pixelsPerSquare);
        var right = new Point(pixelsPerSquare * 7, pixelsPerSquare);
        var bottom = new Point(pixelsPerSquare * 4, pixelsPerSquare * 3);
        var ribbonLayout = CalculateRibbonLayout(ribbon, pixelsPerSquare);
        var cornerScale = pixelsPerSquare / (double)Math.Max(corner.PixelWidth, corner.PixelHeight);
        var junctionScale = pixelsPerSquare / (double)Math.Max(tJoint.PixelWidth, tJoint.PixelHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            DrawRibbon(drawing, ribbon, left, junction, ribbonLayout);
            DrawRibbon(drawing, ribbon, junction, right, ribbonLayout);
            DrawRibbon(drawing, ribbon, junction, bottom, ribbonLayout);
            DrawCorner(drawing, corner, left, 0, cornerScale);
            DrawCorner(drawing, tJoint, junction, 0, junctionScale);
            DrawCorner(drawing, corner, right, 90, cornerScale);
            DrawCorner(drawing, corner, bottom, 180, cornerScale);
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    public static RibbonLayout CalculateRibbonLayout(BitmapSource ribbon, int pixelsPerSquare)
    {
        var (topRow, visibleHeight) = GetAlphaVisibleRows(ribbon);
        var assetScale = pixelsPerSquare / 200d;
        return new RibbonLayout(
            topRow,
            visibleHeight,
            assetScale,
            ribbon.PixelHeight * assetScale,
            visibleHeight * assetScale,
            ribbon.PixelWidth * assetScale);
    }

    public static (int TopRow, int Height) GetAlphaVisibleRows(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var minimum = converted.PixelHeight;
        var maximum = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        {
            var row = y * stride;
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                if (pixels[row + x * 4 + 3] <= 10) continue;
                minimum = Math.Min(minimum, y);
                maximum = Math.Max(maximum, y);
                break;
            }
        }
        return maximum < minimum
            ? (0, converted.PixelHeight)
            : (minimum, maximum - minimum + 1);
    }

    public static void SavePng(BitmapSource bitmap, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(destination);
        encoder.Save(stream);
    }

    private static void DrawRibbon(
        DrawingContext drawing,
        BitmapSource ribbon,
        Point start,
        Point end,
        RibbonLayout layout)
    {
        var delta = end - start;
        var length = delta.Length;
        var angle = Math.Atan2(delta.Y, delta.X) * 180d / Math.PI;
        var tileWidth = layout.RepeatDistance;
        var tileHeight = layout.CanvasThickness;
        drawing.PushTransform(new TranslateTransform(start.X, start.Y));
        drawing.PushTransform(new RotateTransform(angle));
        drawing.PushClip(new RectangleGeometry(new Rect(0, -tileHeight / 2d, length, tileHeight)));
        for (var x = 0d; x < length; x += tileWidth)
            drawing.DrawImage(ribbon, new Rect(x, -tileHeight / 2d, tileWidth, tileHeight));
        drawing.Pop();
        drawing.Pop();
        drawing.Pop();
    }

    private static void DrawRibbon(DrawingContext drawing, BitmapSource ribbon, Point start, Point end,
        RibbonLayout layout, bool fadeStart, bool fadeEnd, double fadePixels)
    {
        var delta = end - start;
        var length = delta.Length;
        if (length < 1) return;
        var angle = Math.Atan2(delta.Y, delta.X) * 180d / Math.PI;
        var tileWidth = layout.RepeatDistance;
        var tileHeight = layout.CanvasThickness;
        drawing.PushTransform(new TranslateTransform(start.X, start.Y));
        drawing.PushTransform(new RotateTransform(angle));
        drawing.PushClip(new RectangleGeometry(new Rect(0, -tileHeight / 2d, length, tileHeight)));
        if (fadeStart || fadeEnd)
        {
            var fraction = Math.Min(.45, Math.Max(0, fadePixels) / length);
            var mask = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
            mask.GradientStops.Add(new GradientStop(fadeStart ? Colors.Transparent : Colors.White, 0));
            if (fadeStart) mask.GradientStops.Add(new GradientStop(Colors.White, fraction));
            if (fadeEnd) mask.GradientStops.Add(new GradientStop(Colors.White, 1 - fraction));
            mask.GradientStops.Add(new GradientStop(fadeEnd ? Colors.Transparent : Colors.White, 1));
            drawing.PushOpacityMask(mask);
        }
        for (var x = 0d; x < length; x += tileWidth)
            drawing.DrawImage(ribbon, new Rect(x, -tileHeight / 2d, tileWidth, tileHeight));
        if (fadeStart || fadeEnd) drawing.Pop();
        drawing.Pop();
        drawing.Pop();
        drawing.Pop();
    }

    private static Dictionary<string, NodePlan> BuildNodes(IReadOnlyList<WallSegment> segments)
    {
        var connections = new Dictionary<string, (Point Center, List<Vector> Directions)>();
        foreach (var segment in segments.Where(segment => segment.HasPrefabEnds))
        {
            Add(segment.Start, segment.End - segment.Start);
            Add(segment.End, segment.Start - segment.End);
        }

        return connections.ToDictionary(pair => pair.Key, pair => PlanNode(pair.Value.Center, pair.Value.Directions));

        void Add(Point point, Vector direction)
        {
            var key = PointKey(point);
            if (!connections.TryGetValue(key, out var node)) node = (point, []);
            if (direction.Length > .1)
            {
                direction.Normalize();
                node.Directions.Add(direction);
            }
            connections[key] = node;
        }
    }

    private static NodePlan PlanNode(Point center, IReadOnlyList<Vector> directions)
    {
        var angles = directions.Select(direction => SnapAngle(Math.Atan2(direction.Y, direction.X) * 180d / Math.PI))
            .Distinct().Order().ToList();
        WallPrefabKind? kind = angles.Count switch
        {
            1 => WallPrefabKind.End,
            2 when AngularSeparation(angles[0], angles[1]) is >= 170 and <= 190 => null,
            2 when AngularSeparation(angles[0], angles[1]) is >= 80 and <= 100 => WallPrefabKind.Corner,
            2 => WallPrefabKind.Diagonal,
            3 => WallPrefabKind.TJoint,
            _ when angles.Count >= 4 => WallPrefabKind.CrossJoint,
            _ => null
        };
        return new NodePlan(center, kind, kind is null ? 0 : BestRotation(kind.Value, angles));
    }

    private static double BestRotation(WallPrefabKind kind, IReadOnlyList<double> target)
    {
        var baseline = kind switch
        {
            WallPrefabKind.End => new[] { 0d },
            WallPrefabKind.Corner => new[] { 0d, 90d },
            WallPrefabKind.Diagonal => new[] { 0d, 135d },
            WallPrefabKind.TJoint => new[] { 0d, 90d, 180d },
            _ => new[] { 0d, 90d, 180d, 270d }
        };
        return Enumerable.Range(0, 8).Select(step => step * 45d)
            .OrderBy(rotation => RotationError(baseline, target, rotation)).First();
    }

    private static double RotationError(IEnumerable<double> baseline, IReadOnlyList<double> target, double rotation) =>
        baseline.Sum(angle => target.Min(candidate => AngularSeparation(NormalizeAngle(angle + rotation), candidate)));
    private static double AngularSeparation(double first, double second)
    {
        var delta = Math.Abs(NormalizeAngle(first) - NormalizeAngle(second));
        return Math.Min(delta, 360 - delta);
    }
    private static double SnapAngle(double angle) => NormalizeAngle(Math.Round(angle / 45d) * 45d);
    private static double NormalizeAngle(double angle) => (angle % 360 + 360) % 360;
    private static string PointKey(Point point) => $"{Math.Round(point.X, 2):0.00}|{Math.Round(point.Y, 2):0.00}";
    private sealed record NodePlan(Point Center, WallPrefabKind? Kind, double Rotation);
    private sealed record RenderElement(string Id, WallPrefabPlacement? Prefab, Action<DrawingContext> Draw);

    private static void DrawCorner(DrawingContext drawing, BitmapSource corner, Point center, double angle, double scale)
    {
        var width = corner.PixelWidth * scale;
        var height = corner.PixelHeight * scale;
        drawing.PushTransform(new RotateTransform(angle, center.X, center.Y));
        drawing.DrawImage(corner, new Rect(center.X - width / 2d, center.Y - height / 2d, width, height));
        drawing.Pop();
    }
}
