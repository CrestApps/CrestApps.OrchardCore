using Microsoft.Extensions.Localization;

namespace CrestApps.Core.AI;

public sealed class AITemplateSourceEntry
{
    public LocalizedString DisplayName { get; set; }

    public LocalizedString Description { get; set; }
}
