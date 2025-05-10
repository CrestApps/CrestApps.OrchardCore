using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core;

public sealed class AIProfileProviderEntry
{
    public AIProfileProviderEntry(string providerName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
