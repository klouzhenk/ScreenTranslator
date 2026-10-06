using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScreenTranslator.Capture;
using ScreenTranslator.Configuration;
using ScreenTranslator.HotKeys;
using System.Windows;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;
    private IDisposable? _hotkeyRegistration;
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddOptions<HotkeyOptions>()
            .Bind(builder.Configuration.GetSection(HotkeyOptions.SectionName));

        builder.Services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        builder.Services.AddSingleton<IMonitorService, MonitorService>();
        builder.Services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();

        _host = builder.Build();
        _host.Start();

        var hotKeyService = _host.Services.GetRequiredService<IHotkeyService>();
        var hotKeyOptions = _host.Services.GetRequiredService<IOptions<HotkeyOptions>>().Value;
        var captureService = _host.Services.GetRequiredService<IScreenCaptureService>();
        var monitorService = _host.Services.GetRequiredService<IMonitorService>();

        _hotkeyRegistration = hotKeyService.Register(
            hotKeyOptions.Select,
            () => OnHotkeyPressed(captureService, monitorService));

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

    private static async Task OnHotkeyPressed(IScreenCaptureService captureService, IMonitorService monitorService)
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

            MessageBox.Show(
                $"X={region.Value.X}, Y={region.Value.Y}, " +
                $"W={region.Value.Width}, H={region.Value.Height}",
                "Selected region");
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
