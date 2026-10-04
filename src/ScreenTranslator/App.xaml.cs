using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScreenTranslator.Configuration;
using ScreenTranslator.HotKeys;
using System.Windows;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;
    private IDisposable? _hotkeyRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddOptions<HotkeyOptions>()
            .Bind(builder.Configuration.GetSection(HotkeyOptions.SectionName));

        builder.Services.AddSingleton<IHotkeyService, GlobalHotkeyService>();

        _host = builder.Build();
        _host.Start();

        var hotKeyService = _host.Services.GetRequiredService<IHotkeyService>();
        var hotKeyOptions = _host.Services.GetRequiredService<IOptions<HotkeyOptions>>().Value;

        _hotkeyRegistration = hotKeyService.Register(
            hotKeyOptions.Select,
            () => MessageBox.Show(
                $"Hotkey fired: {hotKeyOptions.Select}",
                "ScreenTranslator"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyRegistration?.Dispose();
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }
}
