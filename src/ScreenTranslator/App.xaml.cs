using DeepL;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScreenTranslator.Capture;
using ScreenTranslator.Configuration;
using ScreenTranslator.HotKeys;
using ScreenTranslator.Ocr;
using ScreenTranslator.Translation;
using ScreenTranslator.Translation.Exceptions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;
    private IDisposable? _selectHotkeyRegistration;
    private IDisposable? _repeatLastHotkeyRegistration;
    private TaskbarIcon? _trayIcon;
    private (Int32Rect SelectionScreenRect, Int32Rect MonitorBounds)? _lastSelectedRegion;

    // TODO : refactor it
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        var deepLKey = Environment.GetEnvironmentVariable("SCREENTRANSLATOR_DEEPL_API_KEY")
                    ?? throw new InvalidOperationException(
                        "Environment variable SCREENTRANSLATOR_DEEPL_API_KEY is not set. " +
                        "Add it via System Settings or run: " +
                        "[System.Environment]::SetEnvironmentVariable('SCREENTRANSLATOR_DEEPL_API_KEY', '<your-key>', 'User')");

        builder.Services
            .AddOptions<HotkeyOptions>()
            .Bind(builder.Configuration.GetSection(HotkeyOptions.SectionName));
        builder.Services
            .AddOptions<TranslationOptions>()
            .Bind(builder.Configuration.GetSection(TranslationOptions.SectionName));
        builder.Services
            .AddOptions<ResultWindowOptions>()
            .Bind(builder.Configuration.GetSection(ResultWindowOptions.SectionName));

        builder.Services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        builder.Services.AddSingleton<IMonitorService, MonitorService>();
        builder.Services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();
        builder.Services.AddSingleton<IOcrService, WindowsOcrService>();
        builder.Services.AddSingleton<ITranslationService, DeepLTranslationService>();
        builder.Services.AddSingleton(new DeepLClient(deepLKey));

        _host = builder.Build();
        _host.Start();

        var hotKeyService = _host.Services.GetRequiredService<IHotkeyService>();
        var hotKeyOptions = _host.Services.GetRequiredService<IOptions<HotkeyOptions>>().Value;
        var resultWindowOptions = _host.Services.GetRequiredService<IOptions<ResultWindowOptions>>().Value;
        var captureService = _host.Services.GetRequiredService<IScreenCaptureService>();
        var monitorService = _host.Services.GetRequiredService<IMonitorService>();
        var ocrService = _host.Services.GetRequiredService<IOcrService>();
        var translationService = _host.Services.GetRequiredService<ITranslationService>();

        _selectHotkeyRegistration = hotKeyService.Register(
            hotKeyOptions.Select,
            () => _ = OnHotkeyPressed(captureService, monitorService, ocrService, translationService, resultWindowOptions));

        _repeatLastHotkeyRegistration = hotKeyService.Register(
            hotKeyOptions.RepeatLast,
            () => _ = OnRepeatLastPressed(captureService, ocrService, translationService, resultWindowOptions));

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ForceCreate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _selectHotkeyRegistration?.Dispose();
        _repeatLastHotkeyRegistration?.Dispose();
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }

    private async Task OnHotkeyPressed(
        IScreenCaptureService captureService,
        IMonitorService monitorService,
        IOcrService ocrService,
        ITranslationService translationService,
        ResultWindowOptions resultWindowOptions)
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
            _lastSelectedRegion = (selectionScreenRect, monitor.Bounds);

            var text = await ocrService.RecognizeAsync(cropped);
            if (string.IsNullOrWhiteSpace(text))
            {
                ResultWindow.ShowError(original: null, message: "Текст не розпізнано", resultWindowOptions, selectionScreenRect, monitor.Bounds);
                return;
            }

            string translated;
            try
            {
                translated = await translationService.TranslateAsync(text);
            }
            catch (TranslationException ex)
            {
                ResultWindow.ShowError(text, FormatTranslationError(ex.Kind), resultWindowOptions, selectionScreenRect, monitor.Bounds);
                return;
            }

            ResultWindow.ShowTranslation(text, translated, resultWindowOptions, selectionScreenRect, monitor.Bounds);
        }
        finally
        {
            if (previousForeground != IntPtr.Zero)
                Capture.NativeMethods.SetForegroundWindow(previousForeground);
        }
    }

    private async Task OnRepeatLastPressed(IScreenCaptureService captureService, IOcrService ocrService, ITranslationService translationService, ResultWindowOptions resultWindowOptions)
    {
        if (_lastSelectedRegion is null) return;

        var (selectionScreenRect, monitorBounds) = _lastSelectedRegion.Value;

        var frame = captureService.CaptureRegion(monitorBounds);

        var localRect = new Int32Rect(
            selectionScreenRect.X - monitorBounds.X,
            selectionScreenRect.Y - monitorBounds.Y,
            selectionScreenRect.Width,
            selectionScreenRect.Height);

        var cropped = new CroppedBitmap(frame.Image, localRect);
        var text = await ocrService.RecognizeAsync(cropped);

        if (string.IsNullOrWhiteSpace(text))
        {
            ResultWindow.ShowError(original: null, message: "Текст не розпізнано", resultWindowOptions, selectionScreenRect, monitorBounds);
            return;
        }

        string translated;
        try
        {
            translated = await translationService.TranslateAsync(text);
        }
        catch (TranslationException ex)
        {
            ResultWindow.ShowError(text, FormatTranslationError(ex.Kind), resultWindowOptions, selectionScreenRect, monitorBounds);
            return;
        }

        ResultWindow.ShowTranslation(text, translated, resultWindowOptions, selectionScreenRect, monitorBounds);
    }

    private static string FormatTranslationError(TranslationErrorKind kind) => kind switch
    {
        TranslationErrorKind.Authorization => "Невалідний ключ DeepL",
        TranslationErrorKind.Quota         => "Ліміт DeepL вичерпано на цей місяць",
        TranslationErrorKind.Connection    => "Немає з'єднання",
        _                                  => "Помилка перекладу"
    };

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Налаштування будуть пізніше", "ScreenTranslator");
    }

    private void OnShutdownClick(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }
}
