using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;

namespace PowerMouse.App.Services;

internal sealed class TrayIconService : IDisposable
{
    private const uint CallbackMessage = NativeMethods.WmApp + 0x42;
    private const uint IconId = 1;
    private const nuint SubclassId = 0x504D;
    private const uint OpenCommand = 1;
    private const uint ToggleCommand = 2;
    private const uint ExitCommand = 3;

    private readonly nint window;
    private readonly NativeMethods.SubclassProcedure subclassProcedure;
    private NativeMethods.NotifyIconData iconData;
    private bool disposed;

    public TrayIconService(nint window)
    {
        if (window == 0)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(window));
        }

        this.window = window;
        subclassProcedure = WindowSubclass;

        if (!NativeMethods.SetWindowSubclass(window, subclassProcedure, SubclassId, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not listen for tray icon messages.");
        }

        iconData = CreateIconData();
        if (!NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconAdd, ref iconData))
        {
            NativeMethods.RemoveWindowSubclass(window, subclassProcedure, SubclassId);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the PowerMouse tray icon.");
        }

        iconData.Version = NativeMethods.NotifyIconVersion4;
        NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconSetVersion, ref iconData);
    }

    public event Action? OpenRequested;

    public event Action? ToggleRequested;

    public event Action? ExitRequested;

    public Func<bool>? IsEnabledProvider { get; set; }

    public void ShowNotification(string title, string message)
    {
        if (disposed)
        {
            return;
        }

        iconData.Flags = NativeMethods.NotifyIconInfo;
        iconData.InfoTitle = Truncate(title, 63);
        iconData.Info = Truncate(message, 255);
        iconData.InfoFlags = NativeMethods.NotifyInfo;
        NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconModify, ref iconData);

        iconData.Flags = NativeMethods.NotifyIconMessage |
                         NativeMethods.NotifyIconIcon |
                         NativeMethods.NotifyIconTip |
                         NativeMethods.NotifyIconShowTip;
        iconData.InfoTitle = string.Empty;
        iconData.Info = string.Empty;
        iconData.InfoFlags = 0;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconDelete, ref iconData);
        NativeMethods.RemoveWindowSubclass(window, subclassProcedure, SubclassId);
    }

    private nint WindowSubclass(
        nint targetWindow,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        _ = wParam;
        _ = subclassId;
        _ = referenceData;

        if (message == CallbackMessage)
        {
            var notification = unchecked((uint)(lParam.ToInt64() & 0xFFFF));
            switch (notification)
            {
                case NativeMethods.WmLeftButtonUp:
                case NativeMethods.WmLeftButtonDoubleClick:
                    OpenRequested?.Invoke();
                    break;
                case NativeMethods.WmRightButtonUp:
                    ShowContextMenu();
                    break;
            }

            return 0;
        }

        return NativeMethods.DefSubclassProc(targetWindow, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        try
        {
            var enabled = IsEnabledProvider?.Invoke() == true;
            NativeMethods.AppendMenu(menu, NativeMethods.MenuString, OpenCommand, "Open PowerMouse");
            NativeMethods.AppendMenu(menu, NativeMethods.MenuString, ToggleCommand, enabled ? "Turn off" : "Turn on");
            NativeMethods.AppendMenu(menu, NativeMethods.MenuSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MenuString, ExitCommand, "Exit");

            NativeMethods.SetForegroundWindow(window);
            if (!NativeMethods.GetCursorPos(out var point))
            {
                return;
            }

            var command = NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TrackPopupRightButton | NativeMethods.TrackPopupReturnCommand,
                point.X,
                point.Y,
                0,
                window,
                0);

            switch (command)
            {
                case OpenCommand:
                    OpenRequested?.Invoke();
                    break;
                case ToggleCommand:
                    ToggleRequested?.Invoke();
                    break;
                case ExitCommand:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private NativeMethods.NotifyIconData CreateIconData() => new()
    {
        Size = checked((uint)Marshal.SizeOf<NativeMethods.NotifyIconData>()),
        Window = window,
        Id = IconId,
        Flags = NativeMethods.NotifyIconMessage |
                NativeMethods.NotifyIconIcon |
                NativeMethods.NotifyIconTip |
                NativeMethods.NotifyIconShowTip,
        CallbackMessage = CallbackMessage,
        Icon = NativeMethods.LoadIcon(0, new nint(NativeMethods.IconApplication)),
        Tip = "PowerMouse",
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}

