using Microsoft.UI.Dispatching;
using PowerMouse.App.Services;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Windows;
using PowerMouse.Core.Engine;
using PowerMouse.Core.Models;

namespace PowerMouse.ClickHints.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HintSessionTests
{
    private readonly DispatcherQueue dispatcher = new();
    private readonly PowerMouseSettings settings = new()
    {
        MouseHintsEnabled = true,
        MouseHintsShortcut = new HotkeyGesture { VirtualKey = VirtualKeys.F10, Modifiers = HotkeyModifiers.None },
    };

    [TestInitialize]
    public void Reset()
    {
        UiAutomationElementProvider.Requests.Clear();
        ClickHintsOverlayService.ShowCount = 0;
    }

    [TestMethod]
    public void InspectionRespectsDisabledHintsAndDisposal()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        settings.MouseHintsEnabled = false;
        service.InspectForeground(); dispatcher.Drain();
        Assert.HasCount(0, UiAutomationElementProvider.Requests);
        settings.MouseHintsEnabled = true;
        service.Dispose();
        service.InspectForeground(); dispatcher.Drain();
        Assert.HasCount(0, UiAutomationElementProvider.Requests);
    }

    [TestMethod]
    public void DismissalInvalidatesQueuedInspection()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        service.InspectForeground();
        service.Dismiss(); dispatcher.Drain();
        Assert.HasCount(0, UiAutomationElementProvider.Requests);
        Assert.IsFalse(ClickHintsOverlayService.Visible);
    }

    [TestMethod]
    public void RapidOpenCloseOpenRunsOnlyTheNewestQueuedActivation()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        Toggle(service); Toggle(service); Toggle(service);
        dispatcher.Drain();
        Assert.HasCount(1, UiAutomationElementProvider.Requests);
        Complete(UiAutomationElementProvider.Requests.Dequeue());
        WaitUntil(() => ClickHintsOverlayService.Visible);
        Assert.AreEqual(1, ClickHintsOverlayService.ShowCount);
    }

    [TestMethod]
    public void ClickDuringDiscoveryDismissesAndLateCompletionCannotReshow()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        Toggle(service); dispatcher.Drain();
        var pending = UiAutomationElementProvider.Requests.Dequeue();
        Assert.IsNotNull(ClickHintsDismissalMonitor.DismissAction);
        ClickHintsDismissalMonitor.DismissAction(); dispatcher.Drain();
        Complete(pending);
        DrainContinuations();
        Assert.IsFalse(ClickHintsOverlayService.Visible);
        Assert.AreEqual(0, ClickHintsOverlayService.ShowCount);
    }

    [TestMethod]
    public void TypingDuringDiscoveryDoesNotCloseSessionAndRepeatsStaySuppressed()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        Toggle(service); dispatcher.Drain();
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.KeyA, true)));
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.KeyA, true)));
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.KeyA, false)));
        dispatcher.Drain();
        Complete(UiAutomationElementProvider.Requests.Dequeue());
        WaitUntil(() => ClickHintsOverlayService.Visible);
        Assert.AreEqual(1, ClickHintsOverlayService.ShowCount);
    }

    [TestMethod]
    public void OldDetectionFailureCannotCloseTheNewSession()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        Toggle(service); dispatcher.Drain();
        var old = UiAutomationElementProvider.Requests.Dequeue();
        Toggle(service); Toggle(service); dispatcher.Drain();
        var current = UiAutomationElementProvider.Requests.Dequeue();
        old.SetException(new InvalidOperationException("Old provider failed"));
        Complete(current);
        WaitUntil(() => ClickHintsOverlayService.Visible);
        DrainContinuations();
        Assert.IsTrue(ClickHintsOverlayService.Visible);
        Assert.AreEqual(1, ClickHintsOverlayService.ShowCount);
    }

    [TestMethod]
    public void SettingsDisableInvalidatesQueuedActivationAndBalancesKeyUp()
    {
        using var service = new ClickHintsService(settings, dispatcher);
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.F10, true)));
        settings.MouseHintsEnabled = false;
        service.ApplySettings(); dispatcher.Drain();
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.F10, false)));
        Assert.HasCount(0, UiAutomationElementProvider.Requests);
        Assert.IsFalse(ClickHintsOverlayService.Visible);
    }

    private void WaitUntil(Func<bool> condition) => Assert.IsTrue(SpinWait.SpinUntil(() =>
    {
        dispatcher.Drain();
        return condition();
    }, TimeSpan.FromSeconds(2)));

    private void DrainContinuations()
    {
        var deadline = Environment.TickCount64 + 100;
        while (Environment.TickCount64 < deadline) { dispatcher.Drain(); Thread.Yield(); }
    }

    private static void Complete(TaskCompletionSource<DetectionProviderResult> pending) => pending.SetResult(
        DetectionProviderResult.Success([new DetectedElement("button", new ElementBounds(10, 10, 20, 20),
            ElementInteraction.Invoke, DetectionSource.UiAutomation)]));
    private static KeyboardStroke Stroke(int key, bool down) => new(key, 0, false, down);
    private static void Toggle(ClickHintsService service)
    {
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.F10, true)));
        Assert.IsTrue(service.ProcessKey(Stroke(VirtualKeys.F10, false)));
    }
}
