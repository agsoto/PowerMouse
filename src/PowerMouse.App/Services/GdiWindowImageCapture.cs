using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using PowerMouse.App.Interop;
using PowerMouse.ClickHints.Detection;
using PowerMouse.ClickHints.Windows;

namespace PowerMouse.App.Services;

internal sealed class GdiWindowImageCapture : IWindowImageCapture
{
    private const int MaximumDimension = 2400;

    public ValueTask<CapturedWindowImage> CaptureAsync(
        DetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<CapturedWindowImage>(Task.Run(
            () => Capture(request, cancellationToken),
            cancellationToken));
    }

    private static CapturedWindowImage Capture(
        DetectionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var virtualBounds = new ElementBounds(
            NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenX),
            NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenY),
            NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenWidth),
            NativeMethods.GetSystemMetrics(NativeMethods.SystemMetricVirtualScreenHeight));
        var bounds = Intersect(request.TargetBounds, virtualBounds);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("The target window is outside the visible desktop.");
        }

        var sourceWidth = Math.Max(1, checked((int)Math.Round(bounds.Width)));
        var sourceHeight = Math.Max(1, checked((int)Math.Round(bounds.Height)));
        var scale = Math.Min(1d, MaximumDimension / (double)Math.Max(sourceWidth, sourceHeight));
        var outputWidth = Math.Max(1, checked((int)Math.Round(sourceWidth * scale)));
        var outputHeight = Math.Max(1, checked((int)Math.Round(sourceHeight * scale)));
        var byteCount = checked(outputWidth * outputHeight * 4);

        var screenContext = NativeMethods.GetDC(0);
        var memoryContext = screenContext == 0 ? 0 : NativeMethods.CreateCompatibleDC(screenContext);
        nint bitmap = 0;
        nint previousBitmap = 0;
        try
        {
            if (screenContext == 0 || memoryContext == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not initialize screen capture.");
            }

            var bitmapInfo = new NativeMethods.BitmapInfo
            {
                Header = new NativeMethods.BitmapInfoHeader
                {
                    Size = checked((uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>()),
                    Width = outputWidth,
                    Height = -outputHeight,
                    Planes = 1,
                    BitCount = 32,
                    ImageSize = checked((uint)byteCount),
                },
            };
            bitmap = NativeMethods.CreateDIBSection(
                screenContext,
                in bitmapInfo,
                NativeMethods.DibRgbColors,
                out var bits,
                0,
                0);
            if (bitmap == 0 || bits == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate the screen capture bitmap.");
            }

            previousBitmap = NativeMethods.SelectObject(memoryContext, bitmap);
            _ = NativeMethods.SetStretchBltMode(memoryContext, NativeMethods.StretchModeHalftone);
            var copied = NativeMethods.StretchBlt(
                memoryContext,
                0,
                0,
                outputWidth,
                outputHeight,
                screenContext,
                checked((int)Math.Round(bounds.X)),
                checked((int)Math.Round(bounds.Y)),
                sourceWidth,
                sourceHeight,
                NativeMethods.RasterOperationSourceCopy | NativeMethods.RasterOperationCaptureLayeredWindows);
            if (!copied)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not capture the target window.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var pixels = new byte[byteCount];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                outputWidth,
                outputHeight,
                BitmapAlphaMode.Ignore);
            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());
            return new CapturedWindowImage(softwareBitmap, bounds);
        }
        finally
        {
            if (previousBitmap != 0)
            {
                NativeMethods.SelectObject(memoryContext, previousBitmap);
            }

            if (bitmap != 0)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryContext != 0)
            {
                NativeMethods.DeleteDC(memoryContext);
            }

            if (screenContext != 0)
            {
                _ = NativeMethods.ReleaseDC(0, screenContext);
            }
        }
    }

    private static ElementBounds Intersect(ElementBounds left, ElementBounds right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var intersectionRight = Math.Min(left.Right, right.Right);
        var intersectionBottom = Math.Min(left.Bottom, right.Bottom);
        return new ElementBounds(x, y, intersectionRight - x, intersectionBottom - y);
    }
}
