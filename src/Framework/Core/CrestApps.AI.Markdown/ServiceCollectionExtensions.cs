using CrestApps.AI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.AI.Markdown;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMarkdownServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => RagTextNormalizer.CreateMarkdownReader());
        services.AddSingleton(_ => RagTextNormalizer.CreateDefaultChunker());

        return services;
    }
}
