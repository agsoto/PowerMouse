using System.Runtime.InteropServices;
using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Windows;

public static class ForegroundWindowTarget
{
    public static DetectionRequest? TryCreate(DetectionOptions? options = null)
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == 0 || !NativeMethods.GetWindowRect(window, out var rectangle))
        {
            return null;
        }

        var bounds = new ElementBounds(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right - rectangle.Left,
            rectangle.Bottom - rectangle.Top);
        return bounds.IsEmpty ? null : new DetectionRequest(window, bounds, options);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint window, out NativeRectangle rectangle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }
}
