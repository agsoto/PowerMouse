using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class ClickHintDetectorTests
{
    private static readonly ElementBounds WindowBounds = new(0, 0, 1200, 800);
    private static readonly string[] MergedElementIds = ["button", "ocr-link"];
    private static readonly string[] SortedElementIds = ["first", "second"];

    [TestMethod]
    public async Task SparseSemanticResultsUseOcrAndPreferSemanticOverlap()
    {
        var button = Element("button", new ElementBounds(10, 10, 100, 30));
        var semantic = new StubProvider("UIA", DetectionProviderResult.Success([button]));
        var overlappingText = Element(
            "ocr-button",
            new ElementBounds(20, 15, 50, 15),
            DetectionSource.OpticalCharacterRecognition);
        var separateText = Element(
            "ocr-link",
            new ElementBounds(300, 200, 80, 20),
            DetectionSource.OpticalCharacterRecognition);
        var ocr = new StubProvider("OCR", DetectionProviderResult.Success([overlappingText, separateText]));
        var detector = new ClickHintDetector(semantic, ocr);

        var result = await detector.DetectAsync(Request(minimumSemanticTargets: 2));

        Assert.IsTrue(result.UsedOcrFallback);
        CollectionAssert.AreEqual(MergedElementIds, result.Elements.Select(x => x.Id).ToArray());
        Assert.AreEqual(1, ocr.CallCount);
    }

    [TestMethod]
    public async Task EnoughSemanticResultsSkipOcr()
    {
        var semantic = new StubProvider(
            "UIA",
            DetectionProviderResult.Success([
                Element("first", new ElementBounds(10, 10, 20, 20)),
                Element("second", new ElementBounds(40, 10, 20, 20)),
            ]));
        var ocr = new StubProvider("OCR", DetectionProviderResult.Success([]));
        var detector = new ClickHintDetector(semantic, ocr);

        var result = await detector.DetectAsync(Request(minimumSemanticTargets: 2));

        Assert.IsFalse(result.UsedOcrFallback);
        Assert.AreEqual(2, result.Elements.Count);
        Assert.AreEqual(0, ocr.CallCount);
    }

    [TestMethod]
    public async Task ProviderFailureFallsBackAndIsReported()
    {
        var semantic = new ThrowingProvider();
        var fallback = new StubProvider(
            "OCR",
            DetectionProviderResult.Success([
                Element("text", new ElementBounds(10, 10, 30, 10), DetectionSource.OpticalCharacterRecognition),
            ]));
        var detector = new ClickHintDetector(semantic, fallback);

        var result = await detector.DetectAsync(Request(minimumSemanticTargets: 1));

        Assert.IsTrue(result.UsedOcrFallback);
        Assert.AreEqual("text", result.Elements.Single().Id);
        StringAssert.Contains(result.Diagnostics.Single(), nameof(InvalidOperationException));
    }

    [TestMethod]
    public async Task ProviderTimeoutFallsBackWithoutBlockingTheCaller()
    {
        var semantic = new NeverCompletingProvider();
        var fallback = new StubProvider("OCR", DetectionProviderResult.Success([]));
        var options = new DetectionOptions
        {
            MinimumSemanticTargets = 1,
            SemanticTimeout = TimeSpan.FromMilliseconds(20),
        };
        var detector = new ClickHintDetector(semantic, fallback);

        var result = await detector.DetectAsync(new DetectionRequest(1, WindowBounds, options));

        Assert.IsTrue(result.UsedOcrFallback);
        StringAssert.Contains(result.Diagnostics.Single(), "exceeded");
    }

    [TestMethod]
    public async Task ResultsAreClippedDeduplicatedSortedAndLimited()
    {
        var semantic = new StubProvider(
            "UIA",
            DetectionProviderResult.Success([
                Element("outside", new ElementBounds(1500, 10, 20, 20)),
                Element("second", new ElementBounds(50, 100, 20, 20)),
                Element("first", new ElementBounds(10, 20, 20, 20)),
                Element("first", new ElementBounds(10, 20, 20, 20)),
            ]));
        var options = new DetectionOptions
        {
            EnableOcrFallback = false,
            MaximumTargets = 2,
        };
        var detector = new ClickHintDetector(semantic);

        var result = await detector.DetectAsync(new DetectionRequest(1, WindowBounds, options));

        CollectionAssert.AreEqual(SortedElementIds, result.Elements.Select(x => x.Id).ToArray());
    }

    private static DetectionRequest Request(int minimumSemanticTargets) => new(
        1,
        WindowBounds,
        new DetectionOptions { MinimumSemanticTargets = minimumSemanticTargets });

    private static DetectedElement Element(
        string id,
        ElementBounds bounds,
        DetectionSource source = DetectionSource.UiAutomation) => new(
            id,
            bounds,
            source == DetectionSource.UiAutomation ? ElementInteraction.Invoke : ElementInteraction.ApproximateClick,
            source);

    private sealed class StubProvider(string name, DetectionProviderResult result) : IElementDetectionProvider
    {
        public string Name { get; } = name;

        public int CallCount { get; private set; }

        public ValueTask<DetectionProviderResult> DetectAsync(
            DetectionRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingProvider : IElementDetectionProvider
    {
        public string Name => "Broken";

        public ValueTask<DetectionProviderResult> DetectAsync(
            DetectionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Provider is unavailable.");
    }

    private sealed class NeverCompletingProvider : IElementDetectionProvider
    {
        private readonly TaskCompletionSource<DetectionProviderResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "Slow";

        public ValueTask<DetectionProviderResult> DetectAsync(
            DetectionRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            _ = cancellationToken;
            return new ValueTask<DetectionProviderResult>(completion.Task);
        }
    }
}
