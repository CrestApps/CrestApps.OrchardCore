using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureModelDeploymentSource : IOpenAIDeploymentSource
{
    public AzureModelDeploymentSource(IStringLocalizer<AzureModelDeploymentSource> S)
    {
        DisplayName = S["Azure"];
        Description = S["Azure OpenAI model deployments."];
    }

    public string TechnicalName => AzureOpenAIConstants.AzureDeploymentSourceName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
