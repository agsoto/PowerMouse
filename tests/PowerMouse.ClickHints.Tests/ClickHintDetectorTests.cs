using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class ClickHintDetectorTests
{
    private static readonly ElementBounds WindowBounds = new(0, 0, 1200, 800);
    private static readonly string[] MergedElementIds = ["button", "ocr-link"];
    private static readonly string[] SortedElementIds = ["first", "second"];
    private static readonly string[] CardElementIds = ["card"];

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
    public async Task SemanticAndOcrResultsAreMergedEvenWithManySemanticTargets()
    {
        var semantic = new StubProvider(
            "UIA",
            DetectionProviderResult.Success([
                Element("first", new ElementBounds(10, 10, 20, 20)),
                Element("second", new ElementBounds(40, 10, 20, 20)),
            ]));
        var ocr = new StubProvider(
            "OCR",
            DetectionProviderResult.Success([
                Element("ocr", new ElementBounds(300, 200, 20, 20), DetectionSource.OpticalCharacterRecognition),
            ]));
        var detector = new ClickHintDetector(semantic, ocr);

        var result = await detector.DetectAsync(Request(minimumSemanticTargets: 2));

        Assert.IsTrue(result.UsedOcrFallback);
        Assert.AreEqual(3, result.Elements.Count);
        Assert.AreEqual(1, ocr.CallCount);
    }

    [TestMethod]
    public async Task SparseOnlyOcrModeSkipsOcrForEnoughSemanticTargets()
    {
        var semantic = new StubProvider(
            "UIA",
            DetectionProviderResult.Success([
                Element("first", new ElementBounds(10, 10, 20, 20)),
                Element("second", new ElementBounds(40, 10, 20, 20)),
            ]));
        var ocr = new StubProvider("OCR", DetectionProviderResult.Success([]));
        var detector = new ClickHintDetector(semantic, ocr);

        var result = await detector.DetectAsync(new DetectionRequest(
            1,
            WindowBounds,
            new DetectionOptions
            {
                MinimumSemanticTargets = 2,
                EnableOcrAugmentation = false,
            }));

        Assert.IsFalse(result.UsedOcrFallback);
        Assert.AreEqual(0, ocr.CallCount);
    }

    [TestMethod]
    public async Task OcrTextInsideSemanticVisualBoundsIsRejected()
    {
        var semantic = new StubProvider(
            "UIA",
            DetectionProviderResult.Success([
                new DetectedElement(
                    "card",
                    new ElementBounds(100, 100, 24, 24),
                    ElementInteraction.Invoke,
                    DetectionSource.UiAutomation,
                    visualBounds: new ElementBounds(80, 80, 320, 120)),
            ]));
        var ocr = new StubProvider(
            "OCR",
            DetectionProviderResult.Success([
                Element("card-label", new ElementBounds(150, 125, 80, 20), DetectionSource.OpticalCharacterRecognition),
            ]));
        var detector = new ClickHintDetector(semantic, ocr);

        var result = await detector.DetectAsync(Request(minimumSemanticTargets: 1));

        CollectionAssert.AreEqual(CardElementIds, result.Elements.Select(x => x.Id).ToArray());
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

    [TestMethod]
    public async Task DetectionWaitsForBothProvidersBeforeReturningCompleteTargets()
    {
        var semantic = new ControlledProvider("UIA");
        var ocr = new ControlledProvider("OCR");
        var detection = new ClickHintDetector(semantic, ocr).DetectAsync(Request(1)).AsTask();
        semantic.Complete(Element("button", new ElementBounds(10, 10, 20, 20)));
        Assert.IsFalse(detection.IsCompleted, "Partial UIA results must not be published before OCR.");
        ocr.Complete(Element("text", new ElementBounds(200, 200, 20, 20), DetectionSource.OpticalCharacterRecognition));
        var result = await detection;
        Assert.AreEqual(2, result.Elements.Count);
    }

    [TestMethod]
    public async Task ProviderTimeoutRequestsCancellationOfUnderlyingWork()
    {
        var semantic = new ControlledProvider("UIA");
        var result = await new ClickHintDetector(semantic).DetectAsync(new DetectionRequest(1, WindowBounds,
            new DetectionOptions { SemanticTimeout = TimeSpan.FromMilliseconds(20) }));
        Assert.IsTrue(semantic.Token.IsCancellationRequested);
        Assert.AreEqual(0, result.Elements.Count);
        StringAssert.Contains(result.Diagnostics.Single(), "exceeded");
        semantic.Complete();
    }

    [TestMethod]
    public async Task DismissalCancelsBothProvidersAndCannotReturnLateTargets()
    {
        var semantic = new ControlledProvider("UIA");
        var ocr = new ControlledProvider("OCR");
        using var cancellation = new CancellationTokenSource();
        var detection = new ClickHintDetector(semantic, ocr).DetectAsync(Request(1), cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await detection);
        Assert.IsTrue(semantic.Token.IsCancellationRequested);
        Assert.IsTrue(ocr.Token.IsCancellationRequested);
        semantic.Complete(Element("late", new ElementBounds(10, 10, 20, 20)));
        ocr.Complete();
        Assert.IsTrue(detection.IsCanceled);
    }

    private sealed class ControlledProvider(string name) : IElementDetectionProvider
    {
        private readonly TaskCompletionSource<DetectionProviderResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => name;
        public CancellationToken Token { get; private set; }
        public ValueTask<DetectionProviderResult> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            return new ValueTask<DetectionProviderResult>(completion.Task);
        }
        public void Complete(params DetectedElement[] elements) => completion.SetResult(DetectionProviderResult.Success(elements));
    }

    private static DetectionRequest Request(int minimumSemanticTargets) => new(
        1,
        WindowBounds,
        new DetectionOptions { MinimumSemanticTargets = minimumSemanticTargets });

    [TestMethod]
    public async Task PartiallyVisibleTargetsAreClippedBeforePlacement()
    {
        var semantic = new StubProvider("UIA", DetectionProviderResult.Success([
            Element("clipped", new ElementBounds(-100, -20, 130, 50)),
            Element("outside", new ElementBounds(-100, 100, 20, 20)),
        ]));
        var result = await new ClickHintDetector(semantic).DetectAsync(Request(1));
        Assert.AreEqual(1, result.Elements.Count);
        Assert.AreEqual(new ElementBounds(0, 0, 30, 30), result.Elements[0].Bounds);
        Assert.AreEqual(result.Elements[0].Bounds, result.Elements[0].VisualBounds);
    }

    [TestMethod]
    public async Task EarlierOcrTextCannotDisplaceSemanticControlsAtTargetLimit()
    {
        var semantic = new StubProvider("UIA", DetectionProviderResult.Success([
            Element("button", new ElementBounds(100, 700, 30, 30)),
        ]));
        var ocr = new StubProvider("OCR", DetectionProviderResult.Success([
            Element("text", new ElementBounds(100, 10, 30, 20), DetectionSource.OpticalCharacterRecognition),
        ]));
        var result = await new ClickHintDetector(semantic, ocr).DetectAsync(new DetectionRequest(
            1, WindowBounds, new DetectionOptions { MaximumTargets = 1 }));
        Assert.AreEqual("button", result.Elements.Single().Id);
    }

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
