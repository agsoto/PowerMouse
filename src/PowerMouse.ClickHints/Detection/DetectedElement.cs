namespace PowerMouse.ClickHints.Detection;

public sealed record DetectedElement
{
    public DetectedElement(
        string id,
        ElementBounds bounds,
        ElementInteraction interaction,
        DetectionSource source,
        string? name = null,
        double confidence = 1,
        ElementBounds? visualBounds = null,
        UiaControlType controlType = UiaControlType.Unknown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (bounds.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Element bounds must have a positive finite area.");
        }

        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (visualBounds is { IsEmpty: true })
        {
            throw new ArgumentOutOfRangeException(nameof(visualBounds));
        }

        Id = id;
        Bounds = bounds;
        Interaction = interaction;
        Source = source;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Confidence = confidence;
        VisualBounds = visualBounds ?? bounds;
        ControlType = controlType;
    }

    public string Id { get; }

    public string? Name { get; }

    public ElementBounds Bounds { get; }

    public ElementInteraction Interaction { get; }

    public DetectionSource Source { get; }

    public double Confidence { get; }

    public ElementBounds VisualBounds { get; }

    public UiaControlType ControlType { get; }
}
