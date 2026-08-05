namespace PowerMouse.ClickHints.Detection;

public sealed record DetectionOptions
{
    public static DetectionOptions Default { get; } = new();

    public int MinimumSemanticTargets { get; init; } = 4;

    public int MaximumTargets { get; init; } = 500;

    public bool EnableOcrFallback { get; init; } = true;

    public TimeSpan SemanticTimeout { get; init; } = TimeSpan.FromMilliseconds(800);

    public TimeSpan OcrTimeout { get; init; } = TimeSpan.FromMilliseconds(900);

    public double DuplicateOverlapThreshold { get; init; } = 0.45;

    internal void Validate()
    {
        if (MinimumSemanticTargets < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumSemanticTargets));
        }

        if (MaximumTargets <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumTargets));
        }

        if (SemanticTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(SemanticTimeout));
        }

        if (OcrTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(OcrTimeout));
        }

        if (DuplicateOverlapThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(DuplicateOverlapThreshold));
        }
    }
}
