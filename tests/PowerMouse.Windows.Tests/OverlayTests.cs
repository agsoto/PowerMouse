using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerMouse.App.Interop;
using PowerMouse.App.Services;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Hints;

[assembly: DoNotParallelize]

namespace PowerMouse.Windows.Tests;

[TestClass]
public sealed class OverlayTests
{
    [TestMethod]
    public void FirstFrameIsCompleteAndFilteringAndReopeningReplaceHitTargets()
    {
        using var overlay = new ClickHintsOverlayService();
        var foreground = NativeMethods.GetForegroundWindow();
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenX) + 100;
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenY) + 100;
        var first = Hint("A", x, y);
        var second = Hint("B", x + 200, y + 100);
        overlay.Show([first, second]);
        Assert.IsTrue(overlay.IsHintPoint(x + 10, y + 10));
        Assert.IsTrue(overlay.IsHintPoint(x + 210, y + 110));
        Assert.AreEqual(foreground, NativeMethods.GetForegroundWindow(), "Overlay must never take focus.");
        overlay.Update([second]);
        Assert.IsFalse(overlay.IsHintPoint(x + 10, y + 10));
        Assert.IsTrue(overlay.IsHintPoint(x + 210, y + 110));
        overlay.Hide();
        Assert.IsFalse(overlay.IsHintPoint(x + 210, y + 110));
        overlay.Show([first]);
        Assert.IsTrue(overlay.IsHintPoint(x + 10, y + 10));
        Assert.IsFalse(overlay.IsHintPoint(x + 210, y + 110));
    }

    [TestMethod]
    public void RepeatedFramesReleaseGdiResources()
    {
        using var process = Process.GetCurrentProcess();
        using var overlay = new ClickHintsOverlayService();
        var hints = Enumerable.Range(0, 80).Select(i => Hint(i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            100 + (i % 10 * 70), 100 + (i / 10 * 40))).ToArray();
        overlay.Show(hints);
        var before = GetGuiResources(process.Handle, 0);
        for (var i = 0; i < 30; i++)
        {
            overlay.Update(hints);
            overlay.Hide();
            overlay.Show(hints);
        }
        var after = GetGuiResources(process.Handle, 0);
        Assert.IsTrue(after <= before + 2, $"GDI objects grew from {before} to {after}.");
    }

    [TestMethod]
    [TestCategory("InteractiveDesktop")]
    public void PointerIndicatorRunsWithoutCallerMessagePumpAndCleansUp()
    {
        using (var indicator = new CursorIndicatorService())
        {
            indicator.Update(true, true);
            Assert.IsTrue(SpinWait.SpinUntil(() => FindVisibleIndicator() != 0, TimeSpan.FromSeconds(3)),
                "The indicator must show while the calling UI thread is blocked.");
            indicator.Update(false, true);
            Assert.IsTrue(SpinWait.SpinUntil(() => FindVisibleIndicator() == 0, TimeSpan.FromSeconds(3)));
            indicator.Update(true, true);
            Assert.IsTrue(SpinWait.SpinUntil(() => FindVisibleIndicator() != 0, TimeSpan.FromSeconds(3)));
        }
        Assert.AreEqual(0, FindVisibleIndicator());
        // Recreate to catch leaked class registrations or a surviving worker thread.
        using var second = new CursorIndicatorService();
    }

    [TestMethod]
    [TestCategory("InteractiveDesktop")]
    public void PointerTracksMovementAndStaysInsideMonitorEdges()
    {
        Assert.IsTrue(NativeMethods.GetCursorPos(out var original));
        using var indicator = new CursorIndicatorService();
        try
        {
            indicator.Update(true, true);
            Assert.IsTrue(SpinWait.SpinUntil(() => FindVisibleIndicator() != 0, TimeSpan.FromSeconds(3)));
            var window = FindVisibleIndicator();
            var monitor = NativeMethods.MonitorFromPoint(original, 2);
            var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            Assert.IsTrue(NativeMethods.GetMonitorInfo(monitor, ref info));
            var bounds = info.Monitor;
            var positions = new[]
            {
                new NativeMethods.Point { X = bounds.Left + 100, Y = bounds.Top + 100 },
                new NativeMethods.Point { X = bounds.Right - 1, Y = bounds.Top },
                new NativeMethods.Point { X = bounds.Right - 1, Y = bounds.Bottom - 1 },
                new NativeMethods.Point { X = bounds.Left, Y = bounds.Bottom - 1 },
                new NativeMethods.Point { X = bounds.Left, Y = bounds.Top },
            };
            var maximumLatency = 0d;
            foreach (var position in positions)
            {
                Assert.IsTrue(NativeMethods.GetWindowRect(window, out var before));
                var clock = Stopwatch.StartNew();
                MouseInputService.MoveTo(position.X, position.Y);
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                    NativeMethods.GetWindowRect(window, out var rect) &&
                    (rect.Left != before.Left || rect.Top != before.Top), TimeSpan.FromSeconds(1)));
                maximumLatency = Math.Max(maximumLatency, clock.Elapsed.TotalMilliseconds);
                Assert.IsTrue(NativeMethods.GetWindowRect(window, out var after));
                Assert.IsTrue(after.Left >= bounds.Left && after.Top >= bounds.Top &&
                    after.Right <= bounds.Right && after.Bottom <= bounds.Bottom, "Indicator must stay on-screen at every edge.");
            }
            Console.WriteLine($"Maximum observed application pointer-follow latency across {positions.Length} moves: {maximumLatency:0.0} ms.");
        }
        finally
        {
            NativeMethods.SetCursorPos(original.X, original.Y);
        }
    }

    [TestMethod]
    public void PresentRejectsInvalidWindowInsteadOfSilentlyLosingTheFrame()
    {
        using var frame = new LayeredWindowFrame(16, 16);
        Assert.ThrowsExactly<Win32Exception>(() => frame.Present(0, 0, 0, 0x00FF00FF));
    }

    private static ClickHint Hint(string code, int x, int y) => new(code,
        new DetectedElement(code, new ElementBounds(x, y, 20, 20), ElementInteraction.Invoke, DetectionSource.UiAutomation));

    private static nint FindVisibleIndicator()
    {
        nint match = 0;
        EnumWindows((window, parameter) =>
        {
            _ = GetWindowThreadProcessId(window, out var processId);
            if (processId != Environment.ProcessId || !IsWindowVisible(window)) return true;
            var name = new char[100];
            var length = GetClassName(window, name, name.Length);
            if (new string(name, 0, length) == "PowerMouse.CursorIndicator") match = window;
            return true;
        }, 0);
        return match;
    }

    private delegate bool EnumWindowProc(nint window, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, [Out] char[] name, int maximum);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint process, uint flags);
}
