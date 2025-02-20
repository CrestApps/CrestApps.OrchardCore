using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceDeploymentProvider : IAIDeploymentProvider
{
    public AzureAIInferenceDeploymentProvider(IStringLocalizer<AzureAIInferenceDeploymentProvider> S)
    {
        DisplayName = S["Azure AI Inference"];
        Description = S["Azure AI Inference AI deployments."];
    }

    public string TechnicalName
        => AzureAIInferenceProfileSource.ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
