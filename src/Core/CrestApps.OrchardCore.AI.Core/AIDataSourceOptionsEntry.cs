using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core;

public sealed class AIDataSourceOptionsEntry
{
    public AIDataSourceOptionsEntry(AIDataSourceKey key)
    {
        ProfileSource = key.ProfileSource;
        Type = key.Type;
    }

    public string ProfileSource { get; }

    public string Type { get; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
