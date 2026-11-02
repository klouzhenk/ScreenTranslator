using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreenTranslator.Configuration;

namespace ScreenTranslator.Workflow;

internal static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddScreenTranslationWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ResultWindowOptions>()
            .Bind(configuration.GetSection(ResultWindowOptions.SectionName));
        services.AddSingleton<ILastSelectionStore, InMemoryLastSelectionStore>();
        services.AddSingleton<IScreenTranslationWorkflow, ScreenTranslationWorkflow>();
        return services;
    }
}
