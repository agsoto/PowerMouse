using System.ComponentModel;
using System.Runtime.InteropServices;
using PowerMouse.App.Interop;

namespace PowerMouse.App.Services;

// Draw a complete frame off-screen, then hand it to the compositor in one call.
// Do not combine this with SetLayeredWindowAttributes on the same HWND.
internal sealed class LayeredWindowFrame : IDisposable
{
    private nint bitmap;
    private nint previousBitmap;

    public LayeredWindowFrame(int width, int height)
    {
        Width = width;
        Height = height;
        DeviceContext = NativeMethods.CreateCompatibleDC(0);
        var info = new NativeMethods.BitmapInfo
        {
            Header = new NativeMethods.BitmapInfoHeader
            {
                Size = checked((uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>()),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            },
        };
        bitmap = NativeMethods.CreateDIBSection(DeviceContext, in info, NativeMethods.DibRgbColors, out _, 0, 0);
        if (DeviceContext == 0 || bitmap == 0)
        {
            Dispose();
            throw new Win32Exception("Could not allocate an overlay frame.");
        }

        previousBitmap = NativeMethods.SelectObject(DeviceContext, bitmap);
    }

    public nint DeviceContext { get; private set; }
    public int Width { get; }
    public int Height { get; }

    public void Present(nint window, int x, int y, uint colorKey)
    {
        var position = new NativeMethods.Point { X = x, Y = y };
        var size = new NativeMethods.NativeSize { Width = Width, Height = Height };
        var source = new NativeMethods.Point();
        if (!NativeMethods.UpdateLayeredWindow(window, 0, in position, in size,
                DeviceContext, in source, colorKey, 0, NativeMethods.LayeredWindowColorKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not present the overlay frame.");
        }
    }

    public void Dispose()
    {
        if (previousBitmap != 0)
        {
            NativeMethods.SelectObject(DeviceContext, previousBitmap);
            previousBitmap = 0;
        }

        if (bitmap != 0)
        {
            NativeMethods.DeleteObject(bitmap);
            bitmap = 0;
        }

        if (DeviceContext != 0)
        {
            NativeMethods.DeleteDC(DeviceContext);
            DeviceContext = 0;
        }
    }
}
