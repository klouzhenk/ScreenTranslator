using DeepL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Configuration;

namespace ScreenTranslator.Translation;

public static class TranslationServiceCollectionExtensions
{
    public static IServiceCollection AddDeepLTranslation(this IServiceCollection services, IConfiguration configuration, string apiKey)
    {
        services.AddOptions<TranslationOptions>().Bind(configuration.GetSection(TranslationOptions.SectionName));
        services.AddSingleton(new DeepLClient(apiKey));
        services.AddSingleton<ITranslationService, DeepLTranslationService>();
        return services;
    }
}
