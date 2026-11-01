using Microsoft.Extensions.Logging;
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
    ILastSelectionStore lastSelection,
    ILogger<ScreenTranslationWorkflow> logger) : IScreenTranslationWorkflow
{
    private readonly ResultWindowOptions _resultOptions = resultOptions.Value;

    public async Task RepeatLastAsync()
    {
        logger.LogDebug("Repeat-last hotkey pressed");

        if (lastSelection.Last is not { } region)
        {
            logger.LogInformation("Repeat-last pressed but no previous selection");
            return;
        }

        var (selectionScreenRect, monitorBounds) = region;

        var frame = captureService.CaptureRegion(monitorBounds);

        var localRect = new Int32Rect(
            selectionScreenRect.X - monitorBounds.X,
            selectionScreenRect.Y - monitorBounds.Y,
            selectionScreenRect.Width,
            selectionScreenRect.Height);

        var cropped = new CroppedBitmap(frame.Image, localRect);

        await ProcessRegionAsync(cropped, selectionScreenRect, monitorBounds);
    }

    public async Task SelectAndTranslateAsync()
    {
        logger.LogDebug("Select hotkey pressed");

        var previousForeground = Capture.NativeMethods.GetForegroundWindow();

        try
        {
            var monitor = monitorService.GetMonitorUnderCursor();
            var frame = captureService.CaptureRegion(monitor.Bounds);

            var selectionWindow = new SelectionWindow(frame.Image, monitor.Bounds);
            selectionWindow.Show();

            var region = await selectionWindow.Result;
            if (region is null)
            {
                logger.LogDebug("Selection cancelled");
                return;
            }

            var cropped = new CroppedBitmap(frame.Image, region.Value);
            var selectionScreenRect = new Int32Rect(
                monitor.Bounds.X + region.Value.X,
                monitor.Bounds.Y + region.Value.Y,
                region.Value.Width,
                region.Value.Height);
            lastSelection.Last = new SelectionRegion(selectionScreenRect, monitor.Bounds);

            await ProcessRegionAsync(cropped, selectionScreenRect, monitor.Bounds);
        }
        finally
        {
            if (previousForeground != IntPtr.Zero)
                Capture.NativeMethods.SetForegroundWindow(previousForeground);
        }
    }

    private async Task ProcessRegionAsync(
        BitmapSource cropped,
        Int32Rect selectionScreenRect,
        Int32Rect monitorBounds)
    {
        var text = await ocrService.RecognizeAsync(cropped);
        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogInformation("OCR returned empty text for region {Rect}", selectionScreenRect);
            ResultWindow.ShowError(original: null, message: "Текст не розпізнано", _resultOptions, selectionScreenRect, monitorBounds);
            return;
        }

        string translated;
        try
        {
            translated = await translatorService.TranslateAsync(text);
        }
        catch (TranslationException ex)
        {
            logger.LogWarning(ex, "Translation failed: {Kind}", ex.Kind);
            ResultWindow.ShowError(text, FormatTranslationError(ex.Kind), _resultOptions, selectionScreenRect, monitorBounds);
            return;
        }

        ResultWindow.ShowTranslation(text, translated, _resultOptions, selectionScreenRect, monitorBounds);
    }

    private static string FormatTranslationError(TranslationErrorKind kind) => kind switch
    {
        TranslationErrorKind.Authorization  => "Невалідний ключ DeepL",
        TranslationErrorKind.Quota          => "Ліміт DeepL вичерпано на цей місяць",
        TranslationErrorKind.Connection     => "Немає з'єднання",
        _                                   => "Помилка перекладу"
    };
}
