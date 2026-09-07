using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using DymndBuilder.V1;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var rectangle = BuilderGeometry.Create(BuilderShape.Rectangle, 4, 3, 2, 1, 100);
        Require(rectangle.Intersections.Count == 4, "Rectangle must have four intersections.");
        Require(rectangle.PixelWidth == 600 && rectangle.PixelHeight == 500, "Rectangle canvas dimensions are wrong.");
        Require(BuilderGeometry.OutgoingRotation(rectangle, 0) == 0 && BuilderGeometry.OutgoingRotation(rectangle, 1) == 90,
            "Rectangle corner rotations are wrong.");

        var lShape = BuilderGeometry.Create(BuilderShape.LShape, 6, 5, 3, 2, 100);
        Require(lShape.Intersections.Count == 6 && lShape.Segments().Count() == 6, "L shape must have six closed segments.");

        var ribbon = SolidBitmap(60, 12, Colors.OrangeRed);
        var corner = SolidBitmap(20, 20, Colors.Gold);
        var rendered = WallRenderer.Render(rectangle, ribbon, corner);
        Require(rendered.PixelWidth == 600 && rendered.PixelHeight == 500, "Rendered dimensions are wrong.");
        var pixel = new byte[4];
        rendered.CopyPixels(new System.Windows.Int32Rect(100, 100, 1, 1), pixel, 4, 0);
        Require(pixel[3] > 0, "Intersection center should be opaque.");
        rendered.CopyPixels(new System.Windows.Int32Rect(300, 100, 1, 1), pixel, 4, 0);
        Require(pixel[3] > 0, "Ribbon midpoint must remain centered on its wall segment.");
        rendered.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        Require(pixel[3] == 0, "Canvas background must remain transparent.");
        var layout = WallRenderer.CalculateRibbonLayout(ribbon, 100);
        Require(layout.SourceTopRow == 0 && layout.SourceVisibleHeight == 12,
            "Opaque ribbon alpha bounds are wrong.");
        Require(Math.Abs(layout.AssetScale - 0.5) < 0.001 && Math.Abs(layout.RepeatDistance - 30) < 0.001,
            "Ribbon repeat must follow the native 200 px asset grid, independent of wall thickness.");
        Require(Math.Abs(layout.CanvasThickness - 6) < 0.001 && Math.Abs(layout.VisibleThickness - 6) < 0.001,
            "Ribbon canvas and visible thickness must scale uniformly from the native asset grid.");

        var paddedRibbon = PaddedBitmap(60, 20, 4, 10, Colors.OrangeRed);
        var paddedLayout = WallRenderer.CalculateRibbonLayout(paddedRibbon, 200);
        Require(paddedLayout.SourceTopRow == 4 && paddedLayout.SourceVisibleHeight == 10,
            "Transparent ribbon padding was not detected.");
        Require(Math.Abs(paddedLayout.RepeatDistance - 60) < 0.001 && Math.Abs(paddedLayout.VisibleThickness - 10) < 0.001,
            "Transparent padding must remain part of the native canvas instead of stretching visible rows.");

        var guideRectangle = new GuideShape(DrawingTool.Rectangle, new Point(200, 200), new Point(800, 600));
        var guideCircle = new GuideShape(DrawingTool.Circle, new Point(1000, 600), new Point(1200, 600));
        var guideLine = new GuideShape(DrawingTool.Line, new Point(200, 800), new Point(600, 400));
        Require(guideRectangle.Segments().Count() == 4, "Drawn rectangles must produce four wall segments.");
        Require(guideCircle.Segments().Count() == 96, "Drawn circles must produce a smooth closed ribbon guide.");
        Require(guideLine.Segments().Single().Start == new Point(200, 800), "Drawn line endpoints changed during geometry conversion.");
        Require(Math.Abs(PreviewZoom.FitScale(2400, 1600, 1200, 600) - 0.375) < 0.001,
            "Fit Canvas must use the limiting preview dimension.");
        Require(PreviewZoom.Clamp(0.01) == PreviewZoom.Minimum && PreviewZoom.Clamp(10) == PreviewZoom.Maximum,
            "Preview zoom limits changed unexpectedly.");
        Require(Math.Abs(PreviewZoom.OffsetForPivot(300, 400, 24, 1, 2) - 976) < 0.001,
            "Cursor-centered zoom did not preserve the logical point beneath the pointer.");
        var guideLayout = new GuideLayout([guideRectangle, guideCircle, guideLine], 8, 6, 200, 45);
        var prefabs = new WallPrefabSet(new Dictionary<WallPrefabKind, BitmapSource>
        {
            [WallPrefabKind.Corner] = corner, [WallPrefabKind.End] = corner,
            [WallPrefabKind.Diagonal] = corner, [WallPrefabKind.TJoint] = corner, [WallPrefabKind.CrossJoint] = corner
        });
        var guideRender = WallRenderer.Render(guideLayout, ribbon, prefabs);
        Require(guideRender.PixelWidth == 1600 && guideRender.PixelHeight == 1200,
            "Square-based canvas dimensions did not control export dimensions.");
        guideRender.CopyPixels(new System.Windows.Int32Rect(200, 200, 1, 1), pixel, 4, 0);
        Require(pixel[3] > 0, "A prefab was not centered over a drawn rectangle node.");
        guideRender.CopyPixels(new System.Windows.Int32Rect(1200, 600, 1, 1), pixel, 4, 0);
        Require(pixel[3] > 0, "Closed-circle ribbon did not render at its guide radius.");

        var layered = WallRenderer.RenderWithLayers(guideLayout, ribbon, prefabs);
        Require(layered.LayerOrder.Take(guideLayout.Segments.Count).All(id => id.StartsWith("ribbon:", StringComparison.Ordinal)),
            "Wall ribbons must begin beneath prefabs in the default layer stack.");
        Require(layered.LayerOrder.Last().StartsWith("prefab:", StringComparison.Ordinal) && layered.Prefabs.Count > 0,
            "Generated prefabs must be individually represented in the layer stack.");
        var selectedPrefab = layered.Prefabs[0];
        Require(selectedPrefab.Contains(selectedPrefab.Center), "Prefab hit testing must include its center point.");
        Require(!selectedPrefab.Contains(new Point(selectedPrefab.Center.X + selectedPrefab.Width * 2, selectedPrefab.Center.Y)),
            "Prefab hit testing included a point beyond its transformed bounds.");
        var sentBack = WallLayerOrder.Move(layered.LayerOrder, selectedPrefab.Id, WallLayerMove.SendToBack);
        Require(sentBack[0] == selectedPrefab.Id, "Send to Back did not move the prefab behind every ribbon stroke.");
        var broughtForward = WallLayerOrder.Move(sentBack, selectedPrefab.Id, WallLayerMove.BringForward);
        Require(broughtForward[1] == selectedPrefab.Id, "Bring Forward did not advance the prefab exactly one layer.");
        var broughtFront = WallLayerOrder.Move(broughtForward, selectedPrefab.Id, WallLayerMove.BringToFront);
        Require(broughtFront[^1] == selectedPrefab.Id, "Bring to Front did not move the prefab above the complete stack.");

        if (args.Length >= 4 && args[0].Equals("--render-set", StringComparison.OrdinalIgnoreCase))
        {
            var summary = SutInspector.Inspect(args[1]);
            Require(summary.IsRibbon && !summary.UsesSpray && summary.UsesPatternImage,
                "Selected CSP tool is not the expected non-spray ribbon brush.");
            var realPlan = BuilderGeometry.Create(BuilderShape.Rectangle, 4, 3, 2, 1, 200);
            var realRibbon = WallRenderer.LoadImage(args[2]);
            var realCorner = WallRenderer.LoadImage(args[3]);
            var realRender = WallRenderer.Render(realPlan, realRibbon, realCorner);
            var output = Path.Combine(AppContext.BaseDirectory, "builder-v1-real-set.png");
            WallRenderer.SavePng(realRender, output);
            var realLPlan = BuilderGeometry.Create(BuilderShape.LShape, 6, 5, 3, 2, 200);
            var realLRender = WallRenderer.Render(realLPlan, realRibbon, realCorner);
            var lOutput = Path.Combine(AppContext.BaseDirectory, "builder-v1-real-set-l.png");
            WallRenderer.SavePng(realLRender, lOutput);
            Console.WriteLine(summary);
            Console.WriteLine($"Real-set render: {output}");
            Console.WriteLine($"Real-set L render: {lOutput}");
            if (args.Length >= 5)
            {
                var tJoint = WallRenderer.LoadImage(args[4]);
                var calibration = WallRenderer.RenderJointCalibration(realRibbon, realCorner, tJoint);
                var calibrationOutput = Path.Combine(AppContext.BaseDirectory, "builder-v1-joint-calibration.png");
                WallRenderer.SavePng(calibration, calibrationOutput);
                Console.WriteLine($"Joint calibration render: {calibrationOutput}");
            }
        }

        Console.WriteLine("Builder V1 guide geometry, zoom, prefab layers, ribbon, dimensions, seam fading, and transparency smoke tests passed.");
        return 0;
    }

    private static BitmapSource SolidBitmap(int width, int height, Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B; pixels[index + 1] = color.G; pixels[index + 2] = color.R; pixels[index + 3] = color.A;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource PaddedBitmap(int width, int height, int top, int visibleHeight, Color color)
    {
        var pixels = new byte[width * height * 4];
        for (var y = top; y < top + visibleHeight; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = color.A;
            }
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
