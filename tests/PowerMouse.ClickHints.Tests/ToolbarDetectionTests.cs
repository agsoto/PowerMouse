using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
public sealed class ToolbarDetectionTests
{
    [TestMethod]
    public void ExplorerCommandBarsDoNotCreatePhantomEdgeTargets()
    {
        // Geometry and patterns from the inspected Explorer toolbar. AppBar's
        // Toggle/ExpandCollapse patterns describe the bar, not another button.
        var snapshots = new[]
        {
            Snapshot("commands", null, new(11, 5, 569, 78), UiaControlType.AppBar) with
                { SupportsToggle = true, SupportsExpandCollapse = true },
            Snapshot("navigation", null, new(16, 75, 195, 55), UiaControlType.AppBar) with
                { SupportsToggle = true, SupportsExpandCollapse = true },
            Snapshot("detailsBar", null, new(2225, 65, 164, 64), UiaControlType.AppBar) with
                { SupportsToggle = true, SupportsExpandCollapse = true },
            Button("New", new(16, 8, 138, 72)),
            Button("Cut", new(163, 8, 60, 72)),
            Button("Copy", new(232, 8, 60, 72)),
            Button("Paste", new(301, 8, 60, 72)) with { IsEnabled = false },
            Button("Rename", new(370, 8, 59, 72)),
            Button("Delete", new(439, 8, 60, 72)),
            Button("More options", new(508, 8, 68, 72)),
            Button("Back", new(16, 81, 48, 48)),
            Button("Forward", new(78, 81, 48, 48)),
            Button("Refresh", new(140, 81, 48, 48)),
            Button("Details", new(2236, 68, 140, 61)),
        };

        var result = UiaTargetNormalizer.Classify(snapshots);

        Assert.AreEqual(10, result.Count);
        Assert.IsTrue(result.All(e => e.ControlType == UiaControlType.Button));
        Assert.IsFalse(result.Any(e => e.Name == "Paste"));
        foreach (var button in snapshots.Where(s => s.ControlType == UiaControlType.Button && s.IsEnabled))
            Assert.AreEqual(button.Bounds, result.Single(e => e.Id == button.Id).Bounds);
    }

    [TestMethod]
    public async Task AppBarCannotHideUncoveredTextCommandsFromOcr()
    {
        var snapshots = new[]
        {
            Snapshot("bar", null, new(0, 0, 600, 80), UiaControlType.AppBar) with
                { SupportsToggle = true, SupportsExpandCollapse = true },
            Button("New", new(10, 10, 100, 50)),
        };
        var semantic = new Provider(UiaTargetNormalizer.Classify(snapshots));
        var ocr = new Provider([
            Text("New", new(30, 25, 35, 18)),
            Text("View", new(150, 25, 40, 18)),
            Text("Help", new(230, 25, 40, 18)),
        ]);

        var result = await new ClickHintDetector(semantic, ocr).DetectAsync(
            new DetectionRequest(1, new(0, 0, 800, 600)));

        Assert.AreEqual(3, result.Elements.Count);
        Assert.AreEqual(1, result.Elements.Count(e => e.Name == "New"));
        Assert.AreEqual(2, result.Elements.Count(e => e.Source == DetectionSource.OpticalCharacterRecognition));
    }

    private static UiaElementSnapshot Snapshot(string id, string? name, ElementBounds bounds, UiaControlType type) =>
        new(id, name, bounds, type, true, false, false, false, false, false, false, false, false);

    private static UiaElementSnapshot Button(string name, ElementBounds bounds) =>
        Snapshot(name, name, bounds, UiaControlType.Button) with { SupportsInvoke = true };

    private static DetectedElement Text(string name, ElementBounds bounds) =>
        new("ocr:" + name, bounds, ElementInteraction.ApproximateClick,
            DetectionSource.OpticalCharacterRecognition, name);

    private sealed class Provider(IReadOnlyList<DetectedElement> elements) : IElementDetectionProvider
    {
        public string Name => "Fixture";
        public ValueTask<DetectionProviderResult> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DetectionProviderResult.Success(elements));
    }
}
