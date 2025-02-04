using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekCloudChatProfileSource : IAIChatProfileSource
{
    public const string Key = "DeepSeekCloud";

    public DeepSeekCloudChatProfileSource(IStringLocalizer<DeepSeekCloudChatProfileSource> S)
    {
        DisplayName = S["DeepSeek Cloud Service"];
        Description = S["AI-powered chat using DeepSeek Cloud Service."];
    }

    public string TechnicalName
        => Key;

    public string ProviderName
        => DeepSeekConstants.DeepSeekProviderName;

    public string TechnologyName
        => DeepSeekConstants.TechnologyName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
