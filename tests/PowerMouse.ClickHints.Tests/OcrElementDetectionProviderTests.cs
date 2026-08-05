using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class OcrElementDetectionProviderTests
{
    [TestMethod]
    public async Task EmptyAndLowConfidenceRegionsAreExcluded()
    {
        var recognizer = new StubRecognizer([
            new RecognizedTextRegion("Open", new ElementBounds(10, 10, 40, 20), 0.8),
            new RecognizedTextRegion("Noise", new ElementBounds(50, 10, 40, 20), 0.2),
            new RecognizedTextRegion(" ", new ElementBounds(90, 10, 40, 20), 0.9),
        ]);
        var provider = new OcrElementDetectionProvider(recognizer, minimumConfidence: 0.5);

        var result = await provider.DetectAsync(new DetectionRequest(1, new ElementBounds(0, 0, 500, 500)));

        Assert.IsTrue(result.IsSuccess);
        var element = result.Elements.Single();
        Assert.AreEqual("Open", element.Name);
        Assert.AreEqual(ElementInteraction.ApproximateClick, element.Interaction);
    }

    private sealed class StubRecognizer(IReadOnlyList<RecognizedTextRegion> regions) : IOcrTextRecognizer
    {
        public ValueTask<IReadOnlyList<RecognizedTextRegion>> RecognizeAsync(
            DetectionRequest request,
            CancellationToken cancellationToken = default)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(regions);
        }
    }
}
