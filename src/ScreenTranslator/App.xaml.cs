using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenTranslator.Configuration;
using System.Windows;

namespace ScreenTranslator;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddOptions<HotkeyOptions>()
            .Bind(builder.Configuration.GetSection(HotkeyOptions.SectionName));

        _host = builder.Build();
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }
}
