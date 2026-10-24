#pragma warning disable CA1822 // Test doubles must preserve the production instance API.
// Test doubles isolate the real app session coordinator from desktop and WinUI.
// Native rendering and resource ownership are exercised by PowerMouse.Windows.Tests.
using System.Collections.Concurrent;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

namespace Microsoft.UI.Dispatching
{
    internal sealed class DispatcherQueue : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> actions = new();
        public bool TryEnqueue(Action action) { actions.Enqueue(action); return true; }
        public override void Post(SendOrPostCallback d, object? state) => TryEnqueue(() => d(state));
        public void Drain()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try { while (actions.TryDequeue(out var action)) action(); }
            finally { SetSynchronizationContext(previous); }
        }
    }
}
namespace PowerMouse.App.Interop
{
    internal static class NativeMethods
    {
        internal static nint GetForegroundWindow() => 1;
    }
}
namespace PowerMouse.App.Diagnostics
{
    internal static class Logger
    {
        public static void LogInfo(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message, Exception exception) { }
    }
}
namespace PowerMouse.ClickHints.Windows
{
    internal static class ForegroundWindowTarget
    {
        internal static DetectionRequest TryCreate() => new(1, new ElementBounds(0, 0, 1000, 800),
            new DetectionOptions { SemanticTimeout = TimeSpan.FromSeconds(10) });
    }
    internal sealed class UiAutomationElementProvider : IElementDetectionProvider
    {
        internal static readonly Queue<TaskCompletionSource<DetectionProviderResult>> Requests = new();
        public string Name => "Test UIA";
        public ValueTask<DetectionProviderResult> DetectAsync(DetectionRequest request, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<DetectionProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Enqueue(completion);
            return new ValueTask<DetectionProviderResult>(completion.Task);
        }
    }
    internal sealed class WindowsMediaOcrTextRecognizer(PowerMouse.App.Services.GdiWindowImageCapture capture) : IOcrTextRecognizer
    {
        public ValueTask<IReadOnlyList<RecognizedTextRegion>> RecognizeAsync(DetectionRequest request, CancellationToken cancellationToken = default)
        {
            _ = capture;
            return ValueTask.FromResult<IReadOnlyList<RecognizedTextRegion>>([]);
        }
    }
}
namespace PowerMouse.App.Services
{
    internal sealed class GdiWindowImageCapture { }
    internal static class MouseInputService
    {
        public static void ClickAt(int x, int y) { }
        public static void MoveTo(int x, int y) { }
    }
    internal sealed class ClickHintsOverlayService : IDisposable
    {
        internal static int ShowCount { get; set; }
        internal static bool Visible { get; private set; }
        public nint WindowHandle => 2;
        public event Action<ClickHint>? HintInvoked;
        public bool IsHintPoint(int x, int y) => false;
        public void Show(IReadOnlyList<ClickHint> hints) { ShowCount++; Visible = true; }
        public void Update(IReadOnlyList<ClickHint> hints) { }
        public void Hide() => Visible = false;
        public void Dispose() { HintInvoked = null; Hide(); }
        public void Invoke(ClickHint hint) => HintInvoked?.Invoke(hint);
    }
    internal sealed class ClickHintsDismissalMonitor(Action dismiss, Func<int, int, bool> isHintPoint) : IDisposable
    {
        internal static Action? DismissAction { get; private set; }
        public void Start(nint window) { _ = isHintPoint; DismissAction = dismiss; }
        public void Stop() => DismissAction = null;
        public void Dispose() => Stop();
    }
}
