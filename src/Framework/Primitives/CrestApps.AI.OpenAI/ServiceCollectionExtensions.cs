using CrestApps.AI.Clients;
using CrestApps.AI.OpenAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.OpenAI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAI client provider.
    /// </summary>
    public static IServiceCollection AddOpenAIProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIClientProvider, OpenAIClientProvider>());

        services.AddAIProfile<OpenAICompletionClient>(OpenAIConstants.ImplementationName, OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("OpenAI", "OpenAI");
            o.Description = new LocalizedString("OpenAI", "Use OpenAI models for AI completion.");
        });

        services.AddAIConnectionSource(OpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("OpenAI", "OpenAI");
            o.Description = new LocalizedString("OpenAI", "Use OpenAI models for AI completion.");
        });

        return services;
    }
}
