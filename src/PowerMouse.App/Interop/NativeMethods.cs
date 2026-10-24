using System.Runtime.InteropServices;

namespace PowerMouse.App.Interop;

internal static class NativeMethods
{
    internal const int WhKeyboardLowLevel = 13;
    internal const int WhMouseLowLevel = 14;
    internal const int HcAction = 0;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WmQuit = 0x0012;
    internal const uint WmPaint = 0x000F;
    internal const uint WmTimer = 0x0113;
    internal const uint WmMouseMove = 0x0200;
    internal const uint WmEraseBackground = 0x0014;
    internal const uint WmNonClientHitTest = 0x0084;
    internal const uint WmKeyDown = 0x0100;
    internal const uint WmKeyUp = 0x0101;
    internal const uint WmSysKeyDown = 0x0104;
    internal const uint WmSysKeyUp = 0x0105;
    internal const uint WmApp = 0x8000;
    internal const uint WmLeftButtonUp = 0x0202;
    internal const uint WmLeftButtonDoubleClick = 0x0203;
    internal const uint WmRightButtonUp = 0x0205;
    internal const uint WmLeftButtonDown = 0x0201;
    internal const uint WmRightButtonDown = 0x0204;
    internal const uint WmMiddleButtonDown = 0x0207;
    internal const uint WmXButtonDown = 0x020B;

    internal const uint MouseEventMove = 0x0001;
    internal const uint MouseEventLeftDown = 0x0002;
    internal const uint MouseEventLeftUp = 0x0004;
    internal const uint MouseEventRightDown = 0x0008;
    internal const uint MouseEventRightUp = 0x0010;
    internal const uint MouseEventMiddleDown = 0x0020;
    internal const uint MouseEventMiddleUp = 0x0040;
    internal const uint MouseEventWheel = 0x0800;
    internal const uint InputMouse = 0;
    internal const uint InputKeyboard = 1;
    internal const uint KeyEventKeyUp = 0x0002;

    internal const uint NotifyIconAdd = 0x00000000;
    internal const uint NotifyIconModify = 0x00000001;
    internal const uint NotifyIconDelete = 0x00000002;
    internal const uint NotifyIconSetVersion = 0x00000004;
    internal const uint NotifyIconMessage = 0x00000001;
    internal const uint NotifyIconIcon = 0x00000002;
    internal const uint NotifyIconTip = 0x00000004;
    internal const uint NotifyIconInfo = 0x00000010;
    internal const uint NotifyIconShowTip = 0x00000080;
    internal const uint NotifyIconVersion4 = 4;
    internal const uint NotifyInfo = 0x00000001;

    internal const uint MenuString = 0x00000000;
    internal const uint MenuSeparator = 0x00000800;
    internal const uint TrackPopupRightButton = 0x0002;
    internal const uint TrackPopupReturnCommand = 0x0100;

    internal const int IconApplication = 32512;
    internal const uint ImageIcon = 1;
    internal const uint LoadImageFromFile = 0x00000010;
    internal const uint LoadImageDefaultSize = 0x00000040;
    internal const uint ClassHorizontalRedraw = 0x0002;
    internal const uint ClassVerticalRedraw = 0x0001;
    internal const uint WindowExTopMost = 0x00000008;
    internal const uint WindowExTransparent = 0x00000020;
    internal const uint WindowExToolWindow = 0x00000080;
    internal const uint WindowExLayered = 0x00080000;
    internal const uint WindowExNoActivate = 0x08000000;
    internal const uint WindowPopup = 0x80000000;
    internal const uint LayeredWindowColorKey = 0x00000001;
    internal const uint SetWindowPositionNoActivate = 0x0010;
    internal const uint SetWindowPositionShowWindow = 0x0040;
    internal const uint LayeredWindowAlpha = 0x00000002;
    internal const int ShowWindowHide = 0;
    internal const int ShowWindowNoActivate = 4;
    internal const int HitTestTransparent = -1;
    internal const int HitTestClient = 1;
    internal const int SystemMetricVirtualScreenX = 76;
    internal const int SystemMetricVirtualScreenY = 77;
    internal const int SystemMetricVirtualScreenWidth = 78;
    internal const int SystemMetricVirtualScreenHeight = 79;
    internal const int TransparentBackgroundMode = 1;
    internal const int DrawTextCenter = 0x00000001;
    internal const int DrawTextVerticalCenter = 0x00000004;
    internal const int DrawTextSingleLine = 0x00000020;
    internal const int DrawTextNoPrefix = 0x00000800;
    internal const int NullPen = 8;
    internal const int AlternateFill = 1;
    internal const int PenStyleSolid = 0;
    internal const int FontWeightSemiBold = 600;
    internal const uint DibRgbColors = 0;
    internal const uint RasterOperationSourceCopy = 0x00CC0020;
    internal const uint RasterOperationCaptureLayeredWindows = 0x40000000;
    internal const int StretchModeHalftone = 4;

    internal static readonly nint WindowTopMost = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(nint window, nint screenContext,
        in Point position, in NativeSize size, nint sourceContext, in Point source,
        uint colorKey, nint blend, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nuint SetTimer(nint window, nuint id, uint interval, nint callback);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal NativeRectangle Monitor;
        internal NativeRectangle Work;
        internal uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint HookProcedure(int code, nuint wParam, nint lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint SubclassProcedure(
        nint window,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void WinEventProcedure(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(
        int hookId,
        HookProcedure procedure,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint eventHookModule,
        WinEventProcedure eventProcedure,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint eventHook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetMessage(out Message message, nint window, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(in Message message);

    [DllImport("user32.dll")]
    internal static extern nint DispatchMessage(in Message message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int virtualKey);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    internal static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint LoadImage(
        nint instance,
        string name,
        uint type,
        int desiredWidth,
        int desiredHeight,
        uint loadFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowSubclass(
        nint window,
        SubclassProcedure procedure,
        nuint subclassId,
        nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveWindowSubclass(
        nint window,
        SubclassProcedure procedure,
        nuint subclassId);

    [DllImport("comctl32.dll")]
    internal static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenu(nint menu, uint flags, nuint itemId, string? text);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint TrackPopupMenu(
        nint menu,
        uint flags,
        int x,
        int y,
        int reserved,
        nint window,
        nint rectangle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx(in WindowClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    internal static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateEllipticRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(
        nint window,
        uint colorKey,
        byte alpha,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InvalidateRect(nint window, nint rectangle, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateWindow(nint window);

    [DllImport("user32.dll")]
    internal static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ValidateRect(nint window, nint rectangle);

    [DllImport("user32.dll")]
    internal static extern int FillRect(nint deviceContext, in NativeRectangle rectangle, nint brush);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateDIBSection(
        nint deviceContext,
        in BitmapInfo bitmapInfo,
        uint usage,
        out nint bits,
        nint section,
        uint offset);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(
        nint destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        nint source,
        int sourceX,
        int sourceY,
        uint operation);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool StretchBlt(
        nint destination,
        int destinationX,
        int destinationY,
        int destinationWidth,
        int destinationHeight,
        nint source,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        uint operation);

    [DllImport("gdi32.dll")]
    internal static extern int SetStretchBltMode(nint deviceContext, int mode);

    [DllImport("gdi32.dll")]
    internal static extern nint SelectObject(nint deviceContext, nint gdiObject);

    [DllImport("gdi32.dll")]
    internal static extern nint CreatePen(int style, int width, uint color);

    [DllImport("gdi32.dll")]
    internal static extern nint CreatePolygonRgn([In] Point[] points, int pointCount, int fillMode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FillRgn(nint deviceContext, nint region, nint brush);

    [DllImport("gdi32.dll")]
    internal static extern nint GetStockObject(int objectIndex);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RoundRect(
        nint deviceContext,
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MoveToEx(
        nint deviceContext,
        int x,
        int y,
        nint previousPoint);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LineTo(nint deviceContext, int x, int y);

    [DllImport("gdi32.dll")]
    internal static extern uint SetTextColor(nint deviceContext, uint color);

    [DllImport("gdi32.dll")]
    internal static extern int SetBkMode(nint deviceContext, int mode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint CreateFont(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint characterSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTextExtentPoint32(
        nint deviceContext,
        string text,
        int length,
        out NativeSize size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int DrawText(
        nint deviceContext,
        string text,
        int count,
        ref NativeRectangle rectangle,
        int format);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint window);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint objectHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterClass(string className, nint instance);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        internal uint VirtualKey;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Window;
        internal uint Value;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Point;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSize
    {
        internal int Width;
        internal int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint ImageSize;
        internal int XPixelsPerMeter;
        internal int YPixelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal MouseInput Mouse;

        [FieldOffset(0)]
        internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        internal uint Size;
        internal nint Window;
        internal uint Id;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string Tip;

        internal uint State;
        internal uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string Info;

        internal uint Version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string InfoTitle;

        internal uint InfoFlags;
        internal Guid GuidItem;
        internal nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClassEx
    {
        internal uint Size;
        internal uint Style;
        internal WindowProcedure WindowProcedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint BackgroundBrush;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }
}
