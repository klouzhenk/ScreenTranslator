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
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;
    private IDisposable? _hotkeyRegistration;
    private TaskbarIcon? _trayIcon;

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

        builder.Services.AddOptions<HotkeyOptions>().Bind(builder.Configuration.GetSection(HotkeyOptions.SectionName));
        builder.Services.AddOptions<TranslationOptions>().Bind(builder.Configuration.GetSection(TranslationOptions.SectionName));

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
        var captureService = _host.Services.GetRequiredService<IScreenCaptureService>();
        var monitorService = _host.Services.GetRequiredService<IMonitorService>();
        var ocrService = _host.Services.GetRequiredService<IOcrService>();
        var translationService = _host.Services.GetRequiredService<ITranslationService>();

        _hotkeyRegistration = hotKeyService.Register(
            hotKeyOptions.Select,
            () => _ = OnHotkeyPressed(captureService, monitorService, ocrService, translationService));

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ForceCreate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyRegistration?.Dispose();
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }

    private static async Task OnHotkeyPressed(IScreenCaptureService captureService, IMonitorService monitorService, IOcrService ocrService, ITranslationService translationService)
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
            var text = await ocrService.RecognizeAsync(cropped);
            var translated = await translationService.TranslateAsync(text);

            MessageBox.Show($"{text}\n\n→ {translated}", "Translated");
        }
        finally
        {
            if (previousForeground != IntPtr.Zero)
                Capture.NativeMethods.SetForegroundWindow(previousForeground);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Налаштування будуть пізніше", "ScreenTranslator");
    }

    private void OnShutdownClick(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }
}
