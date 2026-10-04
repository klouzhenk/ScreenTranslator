using System.Windows;

namespace ScreenTranslator.Capture;

public interface IScreenCaptureService
{
    CapturedFrame CaptureRegion(Int32Rect region);
}
