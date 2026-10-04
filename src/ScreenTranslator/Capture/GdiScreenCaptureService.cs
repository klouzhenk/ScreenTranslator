using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Capture;

internal sealed class GdiScreenCaptureService(ILogger<GdiScreenCaptureService> logger) : IScreenCaptureService
{
    public CapturedFrame CaptureRegion(Int32Rect region)
    {
        var desktopHwnd = NativeMethods.GetDesktopWindow();
        var screenDc = NativeMethods.GetDC(desktopHwnd);
        if (screenDc == IntPtr.Zero)
            throw new InvalidOperationException("GetDC(desktop) failed");
        
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousObject = IntPtr.Zero;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
                throw new InvalidOperationException("CreateCompatibleDC failed");

            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
                throw new InvalidOperationException("CreateCompatibleBitmap failed");

            previousObject = NativeMethods.SelectObject(memoryDc, bitmap);

            var res = NativeMethods.BitBlt(
                memoryDc, 0, 0, region.Width, region.Height, 
                screenDc, region.X, region.Y, 
                NativeMethods.SRCCOPY);

            if (!res)
                throw new InvalidOperationException("BitBlt failed");

            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            logger.LogDebug("Captured region {Region}", region);
            return new CapturedFrame(source, region);
        }
        finally
        {
            if (previousObject != IntPtr.Zero)
                NativeMethods.SelectObject(memoryDc, previousObject);
            if (bitmap != IntPtr.Zero)
                NativeMethods.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero)
                NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(desktopHwnd, screenDc);
        }
    }
}
