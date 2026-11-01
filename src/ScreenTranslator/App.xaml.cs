using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScreenTranslator.Capture;
using ScreenTranslator.Configuration;
using ScreenTranslator.HotKeys;
using ScreenTranslator.Logging;
using ScreenTranslator.Ocr;
using ScreenTranslator.Translation;
using ScreenTranslator.Workflow;
using System.Windows;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;
    private IDisposable? _selectHotkeyRegistration;
    private IDisposable? _repeatLastHotkeyRegistration;
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        var deepLKey = Environment.GetEnvironmentVariable("SCREENTRANSLATOR_DEEPL_API_KEY")
                    ?? throw new InvalidOperationException(
                        "Environment variable SCREENTRANSLATOR_DEEPL_API_KEY is not set. " +
                        "Add it via System Settings or run: " +
                        "[System.Environment]::SetEnvironmentVariable('SCREENTRANSLATOR_DEEPL_API_KEY', '<your-key>', 'User')");

        builder.Logging.AddFileLogging();

        builder.Services
            .AddHotkeys(builder.Configuration)
            .AddScreenCapture()
            .AddWindowsOcr()
            .AddScreenTranslationWorkflow(builder.Configuration)
            .AddDeepLTranslation(builder.Configuration, deepLKey);

        _host = builder.Build();
        _host.Start();

        var hotKeyService = _host.Services.GetRequiredService<IHotkeyService>();
        var hotKeyOptions = _host.Services.GetRequiredService<IOptions<HotkeyOptions>>().Value;
        var workflow = _host.Services.GetRequiredService<IScreenTranslationWorkflow>();

        _selectHotkeyRegistration = hotKeyService.Register(hotKeyOptions.Select, () => _ = workflow.SelectAndTranslateAsync());
        _repeatLastHotkeyRegistration = hotKeyService.Register(hotKeyOptions.RepeatLast, () => _ = workflow.RepeatLastAsync());

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

    private void OnShutdownClick(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }
}
