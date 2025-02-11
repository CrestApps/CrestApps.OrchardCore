using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAIDeploymentProvider : IAIDeploymentProvider
{
    public const string ProviderName = "OpenAI";

    public OpenAIDeploymentProvider(IStringLocalizer<OpenAIDeploymentProvider> S)
    {
        DisplayName = S["OpenAI"];
        Description = S["OpenAI deployments."];
    }

    public string TechnicalName
        => ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
