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
        builder.Services.AddSingleton<IScreenTranslationWorkflow, ScreenTranslationWorkflow>();
        builder.Services.AddSingleton<ILastSelectionStore, InMemoryLastSelectionStore>();
        builder.Services.AddSingleton(new DeepLClient(deepLKey));

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

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Налаштування будуть пізніше", "ScreenTranslator");
    }

    private void OnShutdownClick(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }
}
