using Microsoft.Extensions.DependencyInjection;

namespace ScreenTranslator.Capture;

internal static class CaptureServiceCollectionExtensions
{
    public static IServiceCollection AddScreenCapture(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();
        return services;
    }
}
