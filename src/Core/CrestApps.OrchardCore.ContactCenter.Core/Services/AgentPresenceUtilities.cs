using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

internal static class AgentPresenceUtilities
{
    public static AgentPresenceStatus ResolveDefaultReadyState(AgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.QueueIds.Count > 0 || profile.CampaignIds.Count > 0
            ? AgentPresenceStatus.Available
            : AgentPresenceStatus.Offline;
    }
}
