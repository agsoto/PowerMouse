namespace PowerMouse.ClickHints.Detection;

public interface IElementDetectionProvider
{
    string Name { get; }

    ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default);
}
