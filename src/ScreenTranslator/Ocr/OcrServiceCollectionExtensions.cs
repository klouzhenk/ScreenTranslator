using Microsoft.Extensions.DependencyInjection;

namespace ScreenTranslator.Ocr;

public static class OcrServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsOcr(this IServiceCollection services)
    {
        services.AddSingleton<IOcrService, WindowsOcrService>();
        return services;
    }
}
