using System.Windows.Media.Imaging;

namespace ScreenTranslator.Ocr;

internal interface IOcrService
{
    Task<string> RecognizeAsync(BitmapSource image);
}
