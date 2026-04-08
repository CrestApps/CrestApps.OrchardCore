using CrestApps.Core.AI.Markdown.Services;
using CrestApps.Core.AI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.AI.Markdown;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMarkdownServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAITextNormalizer, MarkdownAITextNormalizer>();

        return services;
    }
}
