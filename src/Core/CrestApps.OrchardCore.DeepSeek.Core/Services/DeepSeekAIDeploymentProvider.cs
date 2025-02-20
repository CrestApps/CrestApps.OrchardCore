using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekAIDeploymentProvider : IAIDeploymentProvider
{
    public DeepSeekAIDeploymentProvider(IStringLocalizer<DeepSeekAIDeploymentProvider> S)
    {
        DisplayName = S["DeepSeek"];
        Description = S["DeepSeek AI deployments."];
    }

    public string TechnicalName
        => DeepSeekProfileSource.ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
