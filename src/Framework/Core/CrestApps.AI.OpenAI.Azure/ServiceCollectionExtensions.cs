using CrestApps.AI.OpenAI.Azure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.OpenAI.Azure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure OpenAI client provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIClientProvider, AzureOpenAIClientProvider>());

        services.AddAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Azure OpenAI", "Azure OpenAI");
            o.Description = new LocalizedString("Azure OpenAI", "Use Azure OpenAI models for AI completion.");
        });

        services.AddAIConnectionSource(AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Azure OpenAI", "Azure OpenAI");
            o.Description = new LocalizedString("Azure OpenAI", "Use Azure OpenAI models for AI completion.");
        });

        return services;
    }
}
