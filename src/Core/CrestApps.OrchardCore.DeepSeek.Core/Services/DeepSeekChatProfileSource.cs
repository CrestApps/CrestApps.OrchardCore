using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekChatProfileSource : IAIProfileSource
{
    public DeepSeekChatProfileSource(IStringLocalizer<DeepSeekChatProfileSource> S)
    {
        DisplayName = S["DeepSeek Service"];
        Description = S["AI-powered chat using DeepSeek Service."];
    }

    public string TechnicalName
        => DeepSeekAIDeploymentProvider.ProviderName;

    public string ProviderName
        => DeepSeekAIDeploymentProvider.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
