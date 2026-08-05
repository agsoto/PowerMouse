namespace PowerMouse.ClickHints.Detection;

public interface IOcrTextRecognizer
{
    ValueTask<IReadOnlyList<RecognizedTextRegion>> RecognizeAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default);
}
