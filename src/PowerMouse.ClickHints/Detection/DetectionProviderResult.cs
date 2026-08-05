namespace PowerMouse.ClickHints.Detection;

public sealed record DetectionProviderResult
{
    private DetectionProviderResult(IReadOnlyList<DetectedElement> elements, string? error)
    {
        Elements = elements;
        Error = error;
    }

    public IReadOnlyList<DetectedElement> Elements { get; }

    public string? Error { get; }

    public bool IsSuccess => Error is null;

    public static DetectionProviderResult Success(IEnumerable<DetectedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return new DetectionProviderResult(elements.ToArray(), null);
    }

    public static DetectionProviderResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new DetectionProviderResult([], error.Trim());
    }
}
