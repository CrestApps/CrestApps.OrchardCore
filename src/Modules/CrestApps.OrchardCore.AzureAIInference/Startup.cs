using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AzureAIInference.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AzureAIInference;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIProfile<AzureAIInferenceCompletionClient>(AzureAIInferenceConstants.ImplementationName, AzureAIInferenceConstants.ProviderName, o =>
            {
                o.DisplayName = "Azure AI Inference (GitHub Models)";
                o.Description = "Provides AI profiles using Azure AI Inference (GitHub Models).";
            });

        services
            .AddAIDeploymentProvider(AzureAIInferenceConstants.ProviderName, o =>
            {
                o.DisplayName = "Azure AI Inference";
                o.Description = "Azure AI Inference AI deployments.";
            });
    }
}

