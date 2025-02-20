using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekProfileSource : IAIProfileSource
{
    public const string ProviderTechnicalName = "DeepSeek";

    public const string ImplementationName = "DeepSeek";

    public DeepSeekProfileSource(IStringLocalizer<DeepSeekProfileSource> S)
    {
        DisplayName = S["DeepSeek"];
        Description = S["AI-powered chat using DeepSeek Service."];
    }

    public string TechnicalName
        => ImplementationName;

    public string ProviderName
        => ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
