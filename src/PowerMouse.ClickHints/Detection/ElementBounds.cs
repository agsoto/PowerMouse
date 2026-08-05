namespace PowerMouse.ClickHints.Detection;

public readonly record struct ElementBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty =>
        !double.IsFinite(X) ||
        !double.IsFinite(Y) ||
        !double.IsFinite(Width) ||
        !double.IsFinite(Height) ||
        Width <= 0 ||
        Height <= 0;

    public bool Contains(double x, double y) =>
        !IsEmpty && x >= X && x <= Right && y >= Y && y <= Bottom;

    public double IntersectionOverUnion(ElementBounds other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return 0;
        }

        var intersectionWidth = Math.Max(0, Math.Min(Right, other.Right) - Math.Max(X, other.X));
        var intersectionHeight = Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y));
        var intersection = intersectionWidth * intersectionHeight;
        var union = (Width * Height) + (other.Width * other.Height) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
