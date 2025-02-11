using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekProfileSource : IAIProfileSource
{
    public DeepSeekProfileSource(IStringLocalizer<DeepSeekProfileSource> S)
    {
        DisplayName = S["DeepSeek"];
        Description = S["AI-powered chat using DeepSeek Service."];
    }

    public string TechnicalName
        => DeepSeekAIDeploymentProvider.ProviderName;

    public string ProviderName
        => DeepSeekAIDeploymentProvider.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
