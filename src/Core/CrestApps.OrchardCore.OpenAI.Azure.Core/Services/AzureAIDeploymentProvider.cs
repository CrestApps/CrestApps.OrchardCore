using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureAIDeploymentProvider : IAIDeploymentProvider
{
    public AzureAIDeploymentProvider(IStringLocalizer<AzureAIDeploymentProvider> S)
    {
        DisplayName = S["Azure"];
        Description = S["Azure OpenAI model deployments."];
    }

    public string TechnicalName
        => AzureOpenAIConstants.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
