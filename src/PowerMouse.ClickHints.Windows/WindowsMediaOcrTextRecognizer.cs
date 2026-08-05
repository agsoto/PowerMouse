using Windows.Globalization;
using Windows.Media.Ocr;
using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public sealed class WindowsMediaOcrTextRecognizer : IOcrTextRecognizer
{
    private readonly IWindowImageCapture capture;
    private readonly OcrEngine engine;
    private readonly double estimatedConfidence;

    public WindowsMediaOcrTextRecognizer(
        IWindowImageCapture capture,
        string? languageTag = null,
        double estimatedConfidence = 0.5)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (estimatedConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedConfidence));
        }

        this.capture = capture;
        this.estimatedConfidence = estimatedConfidence;
        engine = CreateEngine(languageTag) ?? throw new InvalidOperationException(
            "No compatible Windows OCR language pack is installed.");
    }

    public async ValueTask<IReadOnlyList<RecognizedTextRegion>> RecognizeAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var image = await capture.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
        if (image.Bitmap.PixelWidth <= 0 || image.Bitmap.PixelHeight <= 0)
        {
            return [];
        }

        var result = await engine.RecognizeAsync(image.Bitmap).AsTask(cancellationToken).ConfigureAwait(false);
        var scaleX = image.ScreenBounds.Width / image.Bitmap.PixelWidth;
        var scaleY = image.ScreenBounds.Height / image.Bitmap.PixelHeight;
        return result.Lines
            .Select(line => ToRegion(line, image.ScreenBounds, scaleX, scaleY))
            .Where(region => region is not null)
            .Select(region => region!)
            .ToArray();
    }

    private static OcrEngine? CreateEngine(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }

        var language = new Language(languageTag);
        return OcrEngine.IsLanguageSupported(language)
            ? OcrEngine.TryCreateFromLanguage(language)
            : null;
    }

    private RecognizedTextRegion? ToRegion(
        OcrLine line,
        ElementBounds screenBounds,
        double scaleX,
        double scaleY)
    {
        if (line.Words.Count == 0 || string.IsNullOrWhiteSpace(line.Text))
        {
            return null;
        }

        var left = line.Words.Min(word => word.BoundingRect.X);
        var top = line.Words.Min(word => word.BoundingRect.Y);
        var right = line.Words.Max(word => word.BoundingRect.X + word.BoundingRect.Width);
        var bottom = line.Words.Max(word => word.BoundingRect.Y + word.BoundingRect.Height);
        var bounds = new ElementBounds(
            screenBounds.X + (left * scaleX),
            screenBounds.Y + (top * scaleY),
            (right - left) * scaleX,
            (bottom - top) * scaleY);
        return bounds.IsEmpty ? null : new RecognizedTextRegion(line.Text, bounds, estimatedConfidence);
    }
}
