using Microsoft.Extensions.Localization;

namespace CrestApps.Core.AI;

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
