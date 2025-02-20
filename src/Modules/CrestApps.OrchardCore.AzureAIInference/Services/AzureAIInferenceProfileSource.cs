using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AzureAIInference.Services;

public sealed class AzureAIInferenceProfileSource : IAIProfileSource
{
    public const string ProviderTechnicalName = "AzureAIInference";

    public const string ImplementationName = "AzureAIInference";

    public AzureAIInferenceProfileSource(IStringLocalizer<AzureAIInferenceProfileSource> S)
    {
        DisplayName = S["Azure AI Inference (GitHub Models)"];
        Description = S["Provides AI profiles using Azure AI Inference (GitHub Models)."];
    }

    public string TechnicalName
        => ImplementationName;

    public string ProviderName
        => ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
