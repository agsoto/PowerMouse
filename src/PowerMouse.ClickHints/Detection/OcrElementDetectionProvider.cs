using System.Globalization;

namespace PowerMouse.ClickHints.Detection;

public sealed class OcrElementDetectionProvider : IElementDetectionProvider
{
    private readonly IOcrTextRecognizer recognizer;
    private readonly double minimumConfidence;

    public OcrElementDetectionProvider(IOcrTextRecognizer recognizer, double minimumConfidence = 0.35)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        if (minimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
        }

        this.recognizer = recognizer;
        this.minimumConfidence = minimumConfidence;
    }

    public string Name => "OCR";

    public async ValueTask<DetectionProviderResult> DetectAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var regions = await recognizer.RecognizeAsync(request, cancellationToken).ConfigureAwait(false);
        var elements = regions
            .Where(region =>
                !string.IsNullOrWhiteSpace(region.Text) &&
                !region.Bounds.IsEmpty &&
                region.Confidence >= minimumConfidence &&
                region.Confidence <= 1)
            .Select((region, index) => new DetectedElement(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"ocr:{index}:{region.Bounds.X:0}:{region.Bounds.Y:0}"),
                region.Bounds,
                ElementInteraction.ApproximateClick,
                DetectionSource.OpticalCharacterRecognition,
                region.Text,
                region.Confidence))
            .ToArray();
        return DetectionProviderResult.Success(elements);
    }
}
