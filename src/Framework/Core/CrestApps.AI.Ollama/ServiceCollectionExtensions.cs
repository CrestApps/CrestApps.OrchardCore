using CrestApps.AI.Ollama.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.Ollama;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Ollama AI client provider.
    /// </summary>
    public static IServiceCollection AddOllamaProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIClientProvider, OllamaAIClientProvider>());

        services.AddAIProfile<OllamaCompletionClient>(OllamaConstants.ImplementationName, OllamaConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Ollama", "Ollama");
            o.Description = new LocalizedString("Ollama", "Use locally hosted Ollama models for AI completion.");
        });

        services.AddAIConnectionSource(OllamaConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Ollama", "Ollama");
            o.Description = new LocalizedString("Ollama", "Use locally hosted Ollama models for AI completion.");
        });

        return services;
    }
}
