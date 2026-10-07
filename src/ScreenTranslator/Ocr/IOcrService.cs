using System.Windows.Media.Imaging;

namespace ScreenTranslator.Ocr;

public interface IOcrService
{
    Task<string> RecognizeAsync(BitmapSource image);
}
