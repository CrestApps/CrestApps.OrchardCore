using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceProfileSource : IAIProfileSource
{
    public AzureAIInferenceProfileSource(IStringLocalizer<AzureAIInferenceProfileSource> S)
    {
        DisplayName = S["Azure AI Inference (GitHub Models)"];
        Description = S["Provides AI profiles using Azure AI Inference (GitHub Models)."];
    }

    public string TechnicalName
        => AzureAIInferenceDeploymentProvider.ProviderName;

    public string ProviderName
        => AzureAIInferenceDeploymentProvider.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
