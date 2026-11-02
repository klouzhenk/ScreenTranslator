using System.Windows;

namespace ScreenTranslator.Capture;

internal interface IScreenCaptureService
{
    CapturedFrame CaptureRegion(Int32Rect region);
}
