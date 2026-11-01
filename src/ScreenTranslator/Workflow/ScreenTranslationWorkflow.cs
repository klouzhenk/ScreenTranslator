using Microsoft.Extensions.Options;
using ScreenTranslator.Capture;
using ScreenTranslator.Configuration;
using ScreenTranslator.Ocr;
using ScreenTranslator.Translation;
using ScreenTranslator.Translation.Exceptions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Workflow;

internal sealed class ScreenTranslationWorkflow(
    IScreenCaptureService captureService,
    IMonitorService monitorService,
    IOcrService ocrService,
    ITranslationService translatorService,
    IOptions<ResultWindowOptions> resultOptions,
    ILastSelectionStore lastSelection) : IScreenTranslationWorkflow
{
    private readonly ResultWindowOptions _resultOptions = resultOptions.Value;

    public async Task RepeatLastAsync()
    {
        if (lastSelection.Last is not { } region) return;

        var (selectionScreenRect, monitorBounds) = region;

        var frame = captureService.CaptureRegion(monitorBounds);

        var localRect = new Int32Rect(
            selectionScreenRect.X - monitorBounds.X,
            selectionScreenRect.Y - monitorBounds.Y,
            selectionScreenRect.Width,
            selectionScreenRect.Height);

        var cropped = new CroppedBitmap(frame.Image, localRect);

        await ProcessRegionAsync(cropped, selectionScreenRect, monitorBounds, ocrService, translatorService, _resultOptions);
    }

    public async Task SelectAndTranslateAsync()
    {
        var previousForeground = Capture.NativeMethods.GetForegroundWindow();

        try
        {
            var monitor = monitorService.GetMonitorUnderCursor();
            var frame = captureService.CaptureRegion(monitor.Bounds);

            var selectionWindow = new SelectionWindow(frame.Image, monitor.Bounds);
            selectionWindow.Show();

            var region = await selectionWindow.Result;
            if (region is null) return;

            var cropped = new CroppedBitmap(frame.Image, region.Value);
            var selectionScreenRect = new Int32Rect(
                monitor.Bounds.X + region.Value.X,
                monitor.Bounds.Y + region.Value.Y,
                region.Value.Width,
                region.Value.Height);
            lastSelection.Last = new SelectionRegion(selectionScreenRect, monitor.Bounds);

            await ProcessRegionAsync(cropped, selectionScreenRect, monitor.Bounds, ocrService, translatorService, _resultOptions);
        }
        finally
        {
            if (previousForeground != IntPtr.Zero)
                Capture.NativeMethods.SetForegroundWindow(previousForeground);
        }
    }

    private static async Task ProcessRegionAsync(
        BitmapSource cropped,
        Int32Rect selectionScreenRect,
        Int32Rect monitorBounds,
        IOcrService ocr,
        ITranslationService translator,
        ResultWindowOptions resultOption)
    {
        var text = await ocr.RecognizeAsync(cropped);
        if (string.IsNullOrWhiteSpace(text))
        {
            ResultWindow.ShowError(original: null, message: "Текст не розпізнано", resultOption, selectionScreenRect, monitorBounds);
            return;
        }

        string translated;
        try
        {
            translated = await translator.TranslateAsync(text);
        }
        catch (TranslationException ex)
        {
            ResultWindow.ShowError(text, FormatTranslationError(ex.Kind), resultOption, selectionScreenRect, monitorBounds);
            return;
        }

        ResultWindow.ShowTranslation(text, translated, resultOption, selectionScreenRect, monitorBounds);
    }

    private static string FormatTranslationError(TranslationErrorKind kind) => kind switch
    {
        TranslationErrorKind.Authorization  => "Невалідний ключ DeepL",
        TranslationErrorKind.Quota          => "Ліміт DeepL вичерпано на цей місяць",
        TranslationErrorKind.Connection     => "Немає з'єднання",
        _                                   => "Помилка перекладу"
    };
}
