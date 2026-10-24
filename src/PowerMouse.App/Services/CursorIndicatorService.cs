using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Diagnostics;
using PowerMouse.App.Interop;

namespace PowerMouse.App.Services;

internal sealed class CursorIndicatorService : IDisposable
{
    private const int IndicatorSizeDips = 16;
    private const int IndicatorOffsetDips = 16;
    private const uint TransparentColorKey = 0x00FF00FF;
    private const uint KeyTopColor = 0x00F2F2F2;
    private const uint KeySideColor = 0x00ACACAC;
    private const uint KeyBorderColor = 0x00353535;
    private const uint PointerColor = 0x00303030;
    private const string WindowClassName = "PowerMouse.CursorIndicator";
    private const uint UpdateMessage = NativeMethods.WmApp + 0x71;
    private const uint StopMessage = NativeMethods.WmApp + 0x72;

    private readonly NativeMethods.WindowProcedure windowProcedure;
    private readonly NativeMethods.HookProcedure mouseProcedure;
    private readonly Thread thread;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private LayeredWindowFrame? frame;
    private int indicatorSize;
    private nint window;
    private nint mouseHook;
    private bool updateQueued;
    private NativeMethods.Point pendingPoint;
    private bool hasPendingPoint;
    private bool visible;
    private volatile bool enabled;
    private volatile bool disposed;

    public CursorIndicatorService()
    {
        windowProcedure = WindowCallback;
        mouseProcedure = MouseCallback;
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "PowerMouse pointer indicator",
        };
        thread.Start();
        // Window creation is local and bounded; startup errors are propagated.
        ready.Task.GetAwaiter().GetResult();
        MouseInputService.PointerMoved += RefreshPosition;
    }

    public void Update(bool isActive, bool showIndicator)
    {
        if (disposed) return;
        enabled = isActive && showIndicator;
        RefreshPosition();
    }

    private void RefreshPosition()
    {
        // Keyboard movement and hint jumps notify us AFTER moving the pointer,
        // as NeatMouse's movement notifier does. No timer or hook is required.
        if (!disposed) NativeMethods.PostMessage(window, UpdateMessage, 1, 0);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        MouseInputService.PointerMoved -= RefreshPosition;
        NativeMethods.PostMessage(window, StopMessage, 0, 0);
        thread.Join();
    }

    private void Run()
    {
        var module = NativeMethods.GetModuleHandle(null);
        var registered = false;
        try
        {
            var windowClass = new NativeMethods.WindowClassEx
            {
                Size = checked((uint)Marshal.SizeOf<NativeMethods.WindowClassEx>()),
                WindowProcedure = windowProcedure,
                Instance = module,
                ClassName = WindowClassName,
            };
            if (NativeMethods.RegisterClassEx(in windowClass) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the pointer indicator.");
            registered = true;
            window = NativeMethods.CreateWindowEx(
                NativeMethods.WindowExTopMost | NativeMethods.WindowExTransparent |
                NativeMethods.WindowExToolWindow | NativeMethods.WindowExLayered | NativeMethods.WindowExNoActivate,
                WindowClassName, string.Empty, NativeMethods.WindowPopup,
                0, 0, IndicatorSizeDips, IndicatorSizeDips, 0, 0, module, 0);
            if (window == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the pointer indicator.");

            mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLowLevel, mouseProcedure, module, 0);
            if (mouseHook == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not track pointer movement.");
            // Recovery only: cursor warps, desktop switches, or a removed hook.
            // Normal movement is event driven, independent of the WinUI thread.
            if (NativeMethods.SetTimer(window, 1, 16, 0) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not monitor pointer visibility.");
            ready.SetResult();
            while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(in message);
                NativeMethods.DispatchMessage(in message);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            ready.TrySetException(exception);
            Logger.LogError("The pointer indicator stopped.", exception);
        }
        finally
        {
            if (mouseHook != 0) NativeMethods.UnhookWindowsHookEx(mouseHook);
            frame?.Dispose();
            if (window != 0) NativeMethods.DestroyWindow(window);
            if (registered) NativeMethods.UnregisterClass(WindowClassName, module);
        }
    }

    private nint MouseCallback(int code, nuint wParam, nint lParam)
    {
        // Keep the global hook cheap: no painting, allocation, or synchronous UI calls.
        if (code == NativeMethods.HcAction && (uint)wParam == NativeMethods.WmMouseMove && enabled)
        {
            // MSLLHOOKSTRUCT starts with the new screen point. GetCursorPos
            // here (or in the posted message) can still return the OLD point:
            // Windows commits the move only after the hook chain returns.
            pendingPoint = Marshal.PtrToStructure<NativeMethods.Point>(lParam);
            hasPendingPoint = true;
            if (!updateQueued) updateQueued = NativeMethods.PostMessage(window, UpdateMessage, 0, 0);
        }
        return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private nint WindowCallback(nint targetWindow, uint message, nuint wParam, nint lParam)
    {
        if (message == StopMessage)
        {
            NativeMethods.PostThreadMessage(NativeMethods.GetCurrentThreadId(), NativeMethods.WmQuit, 0, 0);
            return 0;
        }
        if (message is UpdateMessage or NativeMethods.WmTimer)
        {
            updateQueued = false;
            if (message == UpdateMessage && wParam == 1) hasPendingPoint = false;
            try
            {
                UpdatePosition();
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                // Fail open: a display failure must never affect global input.
                NativeMethods.ShowWindow(window, NativeMethods.ShowWindowHide);
                visible = false;
                if (frame is not null)
                {
                    frame.Dispose();
                    frame = null;
                }
                Logger.LogError("Could not update the pointer indicator.", exception);
            }
            return 0;
        }
        if (message == NativeMethods.WmPaint)
        {
            NativeMethods.ValidateRect(targetWindow, 0);
            return 0;
        }
        return NativeMethods.DefWindowProc(targetWindow, message, wParam, lParam);
    }

    private void UpdatePosition()
    {
        var point = pendingPoint;
        var hasPoint = hasPendingPoint;
        hasPendingPoint = false;
        if (!enabled || disposed || (!hasPoint && !NativeMethods.GetCursorPos(out point)))
        {
            if (visible) NativeMethods.ShowWindow(window, NativeMethods.ShowWindowHide);
            visible = false;
            return;
        }

        var monitor = NativeMethods.MonitorFromPoint(point, 2);
        var info = new NativeMethods.MonitorInfo { Size = checked((uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()) };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
        var dpi = NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX : 96u;
        var scale = dpi / 96d;
        var size = Math.Max(1, checked((int)Math.Round(IndicatorSizeDips * scale)));
        var offset = checked((int)Math.Round(IndicatorOffsetDips * scale));
        // Flip beside the pointer at monitor edges instead of leaving the screen.
        var x = point.X + offset + size <= info.Monitor.Right ? point.X + offset : point.X - offset - size;
        var y = point.Y + offset + size <= info.Monitor.Bottom ? point.Y + offset : point.Y - offset - size;
        x = Math.Clamp(x, info.Monitor.Left, Math.Max(info.Monitor.Left, info.Monitor.Right - size));
        y = Math.Clamp(y, info.Monitor.Top, Math.Max(info.Monitor.Top, info.Monitor.Bottom - size));

        if (frame is null || size != indicatorSize)
        {
            frame?.Dispose();
            frame = null;
            indicatorSize = size;
            var nextFrame = new LayeredWindowFrame(size, size);
            try
            {
                Paint(nextFrame);
                nextFrame.Present(window, x, y, TransparentColorKey);
                frame = nextFrame;
            }
            catch
            {
                nextFrame.Dispose();
                throw;
            }
        }
        if (!NativeMethods.SetWindowPos(window, NativeMethods.WindowTopMost, x, y, size, size,
                NativeMethods.SetWindowPositionNoActivate | NativeMethods.SetWindowPositionShowWindow))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not position the pointer indicator.");
        visible = true;
    }

    private void Paint(LayeredWindowFrame frame)
    {
        var deviceContext = frame.DeviceContext;
        if (deviceContext == 0)
        {
            return;
        }

        var transparentBrush = NativeMethods.CreateSolidBrush(TransparentColorKey);
        var keyTopBrush = NativeMethods.CreateSolidBrush(KeyTopColor);
        var keySideBrush = NativeMethods.CreateSolidBrush(KeySideColor);
        var pointerBrush = NativeMethods.CreateSolidBrush(PointerColor);
        var keyBorderPen = NativeMethods.CreatePen(NativeMethods.PenStyleSolid, 1, KeyBorderColor);
        try
        {
            var client = new NativeMethods.NativeRectangle { Right = indicatorSize, Bottom = indicatorSize };

            _ = NativeMethods.FillRect(deviceContext, in client, transparentBrush);
            var oldBrush = NativeMethods.SelectObject(deviceContext, keySideBrush);
            var oldPen = NativeMethods.SelectObject(deviceContext, keyBorderPen);
            NativeMethods.RoundRect(deviceContext, 2, 2, indicatorSize - 2, indicatorSize - 2, 4, 4);

            NativeMethods.SelectObject(deviceContext, keyTopBrush);
            NativeMethods.RoundRect(deviceContext, 2, 2, indicatorSize - 2, indicatorSize - 5, 4, 4);

            NativeMethods.SelectObject(deviceContext, pointerBrush);
            var scale = indicatorSize / 16d;
            var points = new[]
            {
                new NativeMethods.Point { X = checked((int)Math.Round(7 * scale)), Y = checked((int)Math.Round(5 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(7 * scale)), Y = checked((int)Math.Round(13 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(9 * scale)), Y = checked((int)Math.Round(11 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(11 * scale)), Y = checked((int)Math.Round(14 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(13 * scale)), Y = checked((int)Math.Round(13 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(11 * scale)), Y = checked((int)Math.Round(10 * scale)) },
                new NativeMethods.Point { X = checked((int)Math.Round(14 * scale)), Y = checked((int)Math.Round(10 * scale)) },
            };
            var cursorRegion = NativeMethods.CreatePolygonRgn(points, points.Length, NativeMethods.AlternateFill);
            if (cursorRegion != 0)
            {
                NativeMethods.FillRgn(deviceContext, cursorRegion, pointerBrush);
                NativeMethods.DeleteObject(cursorRegion);
            }

            NativeMethods.SelectObject(deviceContext, oldBrush);
            NativeMethods.SelectObject(deviceContext, oldPen);
        }
        finally
        {
            NativeMethods.DeleteObject(keyBorderPen);
            NativeMethods.DeleteObject(pointerBrush);
            NativeMethods.DeleteObject(keySideBrush);
            NativeMethods.DeleteObject(keyTopBrush);
            NativeMethods.DeleteObject(transparentBrush);
        }
    }
}
