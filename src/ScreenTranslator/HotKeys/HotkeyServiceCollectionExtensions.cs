using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Configuration;

namespace ScreenTranslator.HotKeys;

public static class HotkeyServiceCollectionExtensions
{
    public static IServiceCollection AddHotkeys(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HotkeyOptions>().Bind(configuration.GetSection(HotkeyOptions.SectionName));
        services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        return services;
    }
}
