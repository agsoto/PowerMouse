using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;

namespace PowerMouse.App.Services;

internal sealed class ClickHintsDismissalMonitor : IDisposable
{
    private readonly NativeMethods.HookProcedure mouseProcedure;
    private readonly NativeMethods.WinEventProcedure foregroundProcedure;
    private readonly Action dismiss;
    private readonly Func<int, int, bool> isHintPoint;
    private nint mouseHook;
    private nint foregroundHook;
    private nint targetWindow;
    private bool disposed;

    public ClickHintsDismissalMonitor(Action dismiss, Func<int, int, bool> isHintPoint)
    {
        ArgumentNullException.ThrowIfNull(dismiss);
        ArgumentNullException.ThrowIfNull(isHintPoint);
        this.dismiss = dismiss;
        this.isHintPoint = isHintPoint;
        mouseProcedure = OnMouse;
        foregroundProcedure = OnForegroundChanged;
    }

    public void Start(nint window)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Stop();
        targetWindow = window;
        mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLowLevel,
            mouseProcedure,
            NativeMethods.GetModuleHandle(null),
            0);
        foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            0,
            foregroundProcedure,
            0,
            0,
            NativeMethods.WinEventOutOfContext);
        if (mouseHook == 0 || foregroundHook == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "Could not monitor Mouse Hints dismissal events.");
        }
    }

    public void Stop()
    {
        targetWindow = 0;
        if (mouseHook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(mouseHook);
            mouseHook = 0;
        }

        if (foregroundHook != 0)
        {
            NativeMethods.UnhookWinEvent(foregroundHook);
            foregroundHook = 0;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
    }

    private nint OnMouse(int code, nuint wParam, nint lParam)
    {
        _ = lParam;
        if (code == NativeMethods.HcAction && unchecked((uint)wParam) is
            NativeMethods.WmLeftButtonDown or
            NativeMethods.WmRightButtonDown or
            NativeMethods.WmMiddleButtonDown or
            NativeMethods.WmXButtonDown)
        {
            var message = unchecked((uint)wParam);
            var leftClickOnHint = message == NativeMethods.WmLeftButtonDown &&
                                  NativeMethods.GetCursorPos(out var point) &&
                                  isHintPoint(point.X, point.Y);
            if (!leftClickOnHint)
            {
                dismiss();
            }
        }

        return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private void OnForegroundChanged(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        _ = hook;
        _ = eventType;
        _ = objectId;
        _ = childId;
        _ = eventThread;
        _ = eventTime;
        if (targetWindow != 0 && window != 0 && window != targetWindow)
        {
            dismiss();
        }
    }
}
