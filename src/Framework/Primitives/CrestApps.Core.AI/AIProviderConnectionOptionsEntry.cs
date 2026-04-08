using Microsoft.Extensions.Localization;

namespace CrestApps.Core.AI;

public sealed class AIProviderConnectionOptionsEntry
{
    public AIProviderConnectionOptionsEntry(string providerName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
