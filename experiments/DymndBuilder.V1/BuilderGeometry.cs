using System.Windows;

namespace DymndBuilder.V1;

public enum BuilderShape
{
    Rectangle,
    LShape
}

public enum DrawingTool
{
    Line,
    Rectangle,
    Circle
}

public sealed record GuideShape(DrawingTool Tool, Point Start, Point End)
{
    public IReadOnlyList<Point> Points()
    {
        if (Tool == DrawingTool.Line) return [Start, End];
        if (Tool == DrawingTool.Rectangle)
            return [Start, new Point(End.X, Start.Y), End, new Point(Start.X, End.Y)];

        var radius = (End - Start).Length;
        return Enumerable.Range(0, 96)
            .Select(index => index * Math.PI * 2d / 96d)
            .Select(angle => new Point(Start.X + Math.Cos(angle) * radius, Start.Y + Math.Sin(angle) * radius))
            .ToList();
    }

    public IEnumerable<WallSegment> Segments()
    {
        var points = Points();
        var closed = Tool is DrawingTool.Rectangle or DrawingTool.Circle;
        var count = closed ? points.Count : points.Count - 1;
        for (var index = 0; index < count; index++)
            yield return new WallSegment(points[index], points[(index + 1) % points.Count], Tool != DrawingTool.Circle);
    }
}

public sealed record WallSegment(Point Start, Point End, bool HasPrefabEnds);

public sealed record GuideLayout(
    IReadOnlyList<GuideShape> Shapes,
    int WidthSquares,
    int HeightSquares,
    int PixelsPerSquare,
    double FadePixels)
{
    public int PixelWidth => WidthSquares * PixelsPerSquare;
    public int PixelHeight => HeightSquares * PixelsPerSquare;
    public IReadOnlyList<WallSegment> Segments => Shapes.SelectMany(shape => shape.Segments()).ToList();
}

public sealed record BuildPlan(IReadOnlyList<Point> Intersections, int PixelWidth, int PixelHeight, int PixelsPerSquare)
{
    public IEnumerable<(Point Start, Point End)> Segments()
    {
        for (var index = 0; index < Intersections.Count; index++)
            yield return (Intersections[index], Intersections[(index + 1) % Intersections.Count]);
    }
}

public static class BuilderGeometry
{
    public static BuildPlan Create(BuilderShape shape, int widthSquares, int heightSquares,
        int insetWidthSquares, int insetHeightSquares, int pixelsPerSquare)
    {
        if (widthSquares < 2 || heightSquares < 2) throw new ArgumentOutOfRangeException(nameof(widthSquares), "Width and height must be at least two squares.");
        if (pixelsPerSquare < 16) throw new ArgumentOutOfRangeException(nameof(pixelsPerSquare), "Pixels per square must be at least 16.");

        var padding = pixelsPerSquare;
        var width = widthSquares * pixelsPerSquare;
        var height = heightSquares * pixelsPerSquare;
        IReadOnlyList<Point> points;
        if (shape == BuilderShape.Rectangle)
        {
            points =
            [
                new(padding, padding), new(padding + width, padding),
                new(padding + width, padding + height), new(padding, padding + height)
            ];
        }
        else
        {
            if (insetWidthSquares < 1 || insetWidthSquares >= widthSquares
                || insetHeightSquares < 1 || insetHeightSquares >= heightSquares)
                throw new ArgumentOutOfRangeException(nameof(insetWidthSquares), "L-shape cutout dimensions must be positive and smaller than the outer dimensions.");
            var cutWidth = insetWidthSquares * pixelsPerSquare;
            var cutHeight = insetHeightSquares * pixelsPerSquare;
            points =
            [
                new(padding, padding), new(padding + width, padding),
                new(padding + width, padding + height), new(padding + width - cutWidth, padding + height),
                new(padding + width - cutWidth, padding + height - cutHeight), new(padding, padding + height - cutHeight)
            ];
        }

        return new BuildPlan(points, width + padding * 2, height + padding * 2, pixelsPerSquare);
    }

    public static double OutgoingRotation(BuildPlan plan, int intersectionIndex)
    {
        var start = plan.Intersections[intersectionIndex];
        var end = plan.Intersections[(intersectionIndex + 1) % plan.Intersections.Count];
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180d / Math.PI;
        return (angle + 360d) % 360d;
    }
}
