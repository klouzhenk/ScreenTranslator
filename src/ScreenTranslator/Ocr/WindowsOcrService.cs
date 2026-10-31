using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ScreenTranslator.Ocr;

internal sealed class WindowsOcrService : IOcrService
{
    private readonly OcrEngine _engine;

    public WindowsOcrService()
    {
        // TODO : maybe we should do it instead of users (?)
        _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? throw new InvalidOperationException("English OCR language pack is not installed. Install via: Add-WindowsCapability -Online -Name 'Language.OCR~~~en-US~0.0.1.0'");
    }

    public async Task<string> RecognizeAsync(BitmapSource image)
    {
        var upscaled = new TransformedBitmap(image, new ScaleTransform(2, 2));
        using var softwareBitmap = await ToSoftwareBitmapAsync(upscaled);
        var result = await _engine.RecognizeAsync(softwareBitmap);
        return string.Join(' ', result.Lines.Select(l => l.Text));
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(BitmapSource image)
    {
        using var ms = new MemoryStream();
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        encoder.Save(ms);
        ms.Position = 0;

        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }
}
