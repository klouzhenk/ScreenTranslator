using System.Windows;

namespace ScreenTranslator.Capture;

internal sealed record MonitorInfo(Int32Rect Bounds, Int32Rect WorkArea);
