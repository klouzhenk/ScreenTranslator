using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Configuration;

namespace ScreenTranslator.Ocr;

internal static class OcrServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsOcr(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OcrOptions>()
            .Bind(configuration.GetSection(OcrOptions.SectionName));
        services.AddSingleton<IOcrService, WindowsOcrService>();
        return services;
    }
}
