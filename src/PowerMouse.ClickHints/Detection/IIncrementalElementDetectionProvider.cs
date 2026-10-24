namespace PowerMouse.ClickHints.Detection;

/// <summary>Publishes immutable, request-local results before completing slower discovery.</summary>
public interface IIncrementalElementDetectionProvider : IElementDetectionProvider
{
    ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        Action<IReadOnlyList<DetectedElement>> reportPartial,
        CancellationToken cancellationToken = default);
}
