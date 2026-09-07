namespace DymndBuilder.V1;

public static class PreviewZoom
{
    public const double Minimum = 0.10;
    public const double Maximum = 4.00;

    public static double Clamp(double scale) => Math.Clamp(scale, Minimum, Maximum);

    public static double FitScale(double canvasWidth, double canvasHeight, double viewportWidth, double viewportHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0) return 1;
        return Clamp(Math.Min(viewportWidth / canvasWidth, viewportHeight / canvasHeight));
    }

    public static double OffsetForPivot(double oldOffset, double pivot, double padding, double oldScale, double newScale)
    {
        if (oldScale <= 0) return oldOffset;
        var logicalPosition = (oldOffset + pivot - padding) / oldScale;
        return Math.Max(0, padding + logicalPosition * newScale - pivot);
    }
}
