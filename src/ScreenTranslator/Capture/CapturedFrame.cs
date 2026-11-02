using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Capture;

internal sealed record CapturedFrame(BitmapSource Image, Int32Rect ScreenRegion);
