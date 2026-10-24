using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class IncrementalDetectionTests
{
    [TestMethod]
    public async Task SlowListDiscoveryRetainsToolbarButtonsAtDeadline()
    {
        var provider = new SlowProvider();
        var result = await new ClickHintDetector(provider).DetectAsync(Request());

        Assert.AreEqual("Copy", result.Elements.Single().Name);
        StringAssert.Contains(result.Diagnostics.Single(), "retained 1 targets");
        Assert.IsTrue(provider.Token.IsCancellationRequested);
        provider.Finish();
    }

    [TestMethod]
    public async Task DismissingSessionDoesNotDisplayPartialToolbar()
    {
        var provider = new SlowProvider();
        using var cancellation = new CancellationTokenSource();
        var detection = new ClickHintDetector(provider).DetectAsync(Request(), cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await detection);
        provider.Finish();
    }

    [TestMethod]
    public async Task CompletedDiscoveryReplacesPartialResult()
    {
        var provider = new SlowProvider();
        var detection = new ClickHintDetector(provider).DetectAsync(new DetectionRequest(1, new(0, 0, 800, 600))).AsTask();
        Assert.IsFalse(detection.IsCompleted);
        provider.Finish(new DetectedElement("complete", new(50, 10, 50, 40),
            ElementInteraction.Invoke, DetectionSource.UiAutomation, "Full result"));

        var result = await detection;
        Assert.AreEqual("complete", result.Elements.Single().Id);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    private static DetectionRequest Request() => new(1, new(0, 0, 800, 600),
        new DetectionOptions { SemanticTimeout = TimeSpan.FromMilliseconds(40) });

    private sealed class SlowProvider : IIncrementalElementDetectionProvider
    {
        private readonly TaskCompletionSource<DetectionProviderResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => "UIA fixture";
        public CancellationToken Token { get; private set; }
        public ValueTask<DetectionProviderResult> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("The incremental overload must be used.");

        public ValueTask<DetectionProviderResult> DetectAsync(DetectionRequest request,
            Action<IReadOnlyList<DetectedElement>> reportPartial, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            reportPartial([new DetectedElement("copy", new(20, 10, 40, 40),
                ElementInteraction.Invoke, DetectionSource.UiAutomation, "Copy", controlType: UiaControlType.Button)]);
            return new ValueTask<DetectionProviderResult>(completion.Task);
        }

        public void Finish(params DetectedElement[] elements) => completion.SetResult(DetectionProviderResult.Success(elements));
    }
}
