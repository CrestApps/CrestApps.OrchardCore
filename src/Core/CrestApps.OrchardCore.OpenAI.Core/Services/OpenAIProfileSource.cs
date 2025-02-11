using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAIProfileSource : IAIProfileSource
{
    public OpenAIProfileSource(IStringLocalizer<OpenAIProfileSource> S)
    {
        DisplayName = S["OpenAI"];
        Description = S["Provides AI profiles using OpenAI."];
    }

    public string TechnicalName
        => OpenAIDeploymentProvider.ProviderName;

    public string ProviderName
        => OpenAIDeploymentProvider.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
