using CrestApps.AI.AzureAIInference.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.AzureAIInference;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure AI Inference (GitHub Models) client provider.
    /// </summary>
    public static IServiceCollection AddAzureAIInferenceProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIClientProvider, AzureAIInferenceClientProvider>());

        services.AddAIProfile<AzureAIInferenceCompletionClient>(AzureAIInferenceConstants.ImplementationName, AzureAIInferenceConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Azure AI Inference", "Azure AI Inference / GitHub Models");
            o.Description = new LocalizedString("Azure AI Inference", "Use Azure AI Inference or GitHub Models for AI completion.");
        });

        services.AddAIConnectionSource(AzureAIInferenceConstants.ProviderName, o =>
        {
            o.DisplayName = new LocalizedString("Azure AI Inference", "Azure AI Inference / GitHub Models");
            o.Description = new LocalizedString("Azure AI Inference", "Use Azure AI Inference or GitHub Models for AI completion.");
        });

        return services;
    }
}
