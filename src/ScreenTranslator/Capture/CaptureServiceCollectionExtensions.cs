using Microsoft.Extensions.DependencyInjection;

namespace ScreenTranslator.Capture;

public static class CaptureServiceCollectionExtensions
{
    public static IServiceCollection AddScreenCapture(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();
        return services;
    }
}
