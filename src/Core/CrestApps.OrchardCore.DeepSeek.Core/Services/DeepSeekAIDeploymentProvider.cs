using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekAIDeploymentProvider : IAIDeploymentProvider
{
    public const string ProviderName = "DeepSeek";

    public DeepSeekAIDeploymentProvider(IStringLocalizer<DeepSeekAIDeploymentProvider> S)
    {
        DisplayName = S["DeepSeek"];
        Description = S["DeepSeek model deployments."];
    }

    public string TechnicalName
        => ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
