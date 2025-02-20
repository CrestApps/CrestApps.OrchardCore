using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAIDeploymentProvider : IAIDeploymentProvider
{
    public OpenAIDeploymentProvider(IStringLocalizer<OpenAIDeploymentProvider> S)
    {
        DisplayName = S["OpenAI"];
        Description = S["OpenAI deployments."];
    }

    public string TechnicalName
        => OpenAIProfileSource.ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
