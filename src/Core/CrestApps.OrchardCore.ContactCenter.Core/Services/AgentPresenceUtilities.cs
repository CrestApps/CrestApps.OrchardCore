using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

internal static class AgentPresenceUtilities
{
    /// <summary>
    /// Resolves the presence state an agent returns to after a call or reservation ends, when no explicit
    /// requested state was captured. An agent who was working intends to keep working, so they return to
    /// <see cref="AgentPresenceStatus.Available"/> regardless of whether they belong to a queue or campaign:
    /// availability is reachability, not queue eligibility (queue offers are gated on queue membership separately),
    /// and a manual or direct-line agent must not be signed out just because they took a call. A signed-out
    /// (<see cref="AgentPresenceStatus.Offline"/>) agent is left offline -- an explicit sign-out, or the
    /// session/heartbeat cleanup that marks a genuinely absent agent offline, must not be undone by a stale
    /// reservation releasing.
    /// </summary>
    public static AgentPresenceStatus ResolveDefaultReadyState(AgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.PresenceStatus == AgentPresenceStatus.Offline
            ? AgentPresenceStatus.Offline
            : AgentPresenceStatus.Available;
    }
}
