using System.Windows;

namespace ScreenTranslator.Capture;

public sealed record MonitorInfo(Int32Rect Bounds, Int32Rect WorkArea);
