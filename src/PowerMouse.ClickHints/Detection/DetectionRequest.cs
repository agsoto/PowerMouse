namespace PowerMouse.ClickHints.Detection;

public sealed record DetectionRequest
{
    public DetectionRequest(nint targetWindow, ElementBounds targetBounds, DetectionOptions? options = null)
    {
        if (targetWindow == 0)
        {
            throw new ArgumentException("A target window handle is required.", nameof(targetWindow));
        }

        if (targetBounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(targetBounds));
        }

        Options = options ?? DetectionOptions.Default;
        Options.Validate();
        TargetWindow = targetWindow;
        TargetBounds = targetBounds;
    }

    public nint TargetWindow { get; }

    public ElementBounds TargetBounds { get; }

    public DetectionOptions Options { get; }
}
