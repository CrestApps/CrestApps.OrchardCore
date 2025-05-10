using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core;

public sealed class AIDataSourceOptionsEntry
{
    public AIDataSourceOptionsEntry(AIDataSourceKey key)
    {
        ProviderName = key.ProviderName;
        Type = key.Type;
    }

    public string ProviderName { get; }

    public string Type { get; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
