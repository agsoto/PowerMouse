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
            .SelectMany(line => OcrWordGrouping.GroupLine(line.Words.Select(word =>
                ToRegion(word, image.ScreenBounds, scaleX, scaleY))))
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

    private RecognizedTextRegion ToRegion(
        OcrWord word,
        ElementBounds screenBounds,
        double scaleX,
        double scaleY)
    {
        var rectangle = word.BoundingRect;
        return new RecognizedTextRegion(word.Text, new ElementBounds(
            screenBounds.X + (rectangle.X * scaleX),
            screenBounds.Y + (rectangle.Y * scaleY),
            rectangle.Width * scaleX,
            rectangle.Height * scaleY), estimatedConfidence);
    }
}
