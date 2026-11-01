using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenTranslator.Capture;

internal sealed class MonitorService : IMonitorService
{
    public MonitorInfo GetMonitorUnderCursor()
    {
        if (!NativeMethods.GetCursorPos(out var point))
            throw new InvalidOperationException("GetCursorPos failed");

        var hMonitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
            throw new InvalidOperationException("MonitorFromPoint returned null");

        var monitorInfo = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (!NativeMethods.GetMonitorInfoW(hMonitor, ref monitorInfo))
            throw new InvalidOperationException("GetMonitorInfo failed");

        return new MonitorInfo(ToRect(monitorInfo.rcMonitor), ToRect(monitorInfo.rcWork));
    }

    private static Int32Rect ToRect(NativeMethods.RECT r)
    {
        return new(r.Left, r.Top, r.Width, r.Height);
    }
}
