using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using PowerMouse.App.Interop;

namespace PowerMouse.App.Services;

internal sealed class CursorIndicatorService : IDisposable
{
    private const int IndicatorSizeDips = 10;
    private const int IndicatorOffsetDips = 16;
    private const string WindowClassName = "PowerMouse.CursorIndicator";

    private readonly NativeMethods.WindowProcedure windowProcedure;
    private readonly nint module;
    private readonly nint brush;
    private readonly DispatcherQueueTimer timer;
    private int indicatorSize = IndicatorSizeDips;
    private int indicatorOffset = IndicatorOffsetDips;
    private nint window;
    private bool active;
    private bool settingEnabled;
    private bool disposed;

    public CursorIndicatorService(DispatcherQueue dispatcherQueue)
    {
        windowProcedure = NativeMethods.DefWindowProc;
        module = NativeMethods.GetModuleHandle(null);
        brush = NativeMethods.CreateSolidBrush(0x00D47800); // #0078D4 in COLORREF format.

        var windowClass = new NativeMethods.WindowClassEx
        {
            Size = checked((uint)Marshal.SizeOf<NativeMethods.WindowClassEx>()),
            Style = NativeMethods.ClassHorizontalRedraw | NativeMethods.ClassVerticalRedraw,
            WindowProcedure = windowProcedure,
            Instance = module,
            BackgroundBrush = brush,
            ClassName = WindowClassName,
        };

        if (NativeMethods.RegisterClassEx(in windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the pointer indicator window.");
        }

        window = NativeMethods.CreateWindowEx(
            NativeMethods.WindowExTopMost |
            NativeMethods.WindowExTransparent |
            NativeMethods.WindowExToolWindow |
            NativeMethods.WindowExLayered |
            NativeMethods.WindowExNoActivate,
            WindowClassName,
            string.Empty,
            NativeMethods.WindowPopup,
            0,
            0,
            IndicatorSizeDips,
            IndicatorSizeDips,
            0,
            0,
            module,
            0);

        if (window == 0)
        {
            NativeMethods.UnregisterClass(WindowClassName, module);
            NativeMethods.DeleteObject(brush);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the pointer indicator window.");
        }

        if (!NativeMethods.SetLayeredWindowAttributes(
                window,
                colorKey: 0,
                alpha: byte.MaxValue,
                flags: NativeMethods.LayeredWindowAlpha))
        {
            NativeMethods.DestroyWindow(window);
            window = 0;
            NativeMethods.UnregisterClass(WindowClassName, module);
            NativeMethods.DeleteObject(brush);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not initialize the pointer indicator window.");
        }

        var dpi = NativeMethods.GetDpiForWindow(window);
        var scale = dpi == 0 ? 1d : dpi / 96d;
        indicatorSize = Math.Max(1, checked((int)Math.Round(IndicatorSizeDips * scale)));
        indicatorOffset = checked((int)Math.Round(IndicatorOffsetDips * scale));

        var region = NativeMethods.CreateEllipticRgn(0, 0, indicatorSize, indicatorSize);
        if (region != 0 && NativeMethods.SetWindowRgn(window, region, redraw: true) == 0)
        {
            NativeMethods.DeleteObject(region);
        }

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += (_, _) => UpdatePosition();
        timer.Start();
    }

    public void Update(bool isActive, bool showIndicator)
    {
        active = isActive;
        settingEnabled = showIndicator;
        UpdatePosition();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Stop();
        if (window != 0)
        {
            NativeMethods.DestroyWindow(window);
            window = 0;
        }

        NativeMethods.UnregisterClass(WindowClassName, module);
        NativeMethods.DeleteObject(brush);
    }

    private void UpdatePosition()
    {
        if (disposed || window == 0)
        {
            return;
        }

        if (!active || !settingEnabled || !NativeMethods.GetCursorPos(out var point))
        {
            NativeMethods.ShowWindow(window, NativeMethods.ShowWindowHide);
            return;
        }

        NativeMethods.SetWindowPos(
            window,
            NativeMethods.WindowTopMost,
            point.X + indicatorOffset,
            point.Y + indicatorOffset,
            indicatorSize,
            indicatorSize,
            NativeMethods.SetWindowPositionNoActivate | NativeMethods.SetWindowPositionShowWindow);
    }
}
