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

    /// <summary>
    /// Keeps <see cref="AgentProfile.IdleSinceUtc"/> in step with the agent's state: an agent who is Available
    /// with nothing reserved is idle, and the clock starts the moment they become so; anyone else is not idle and
    /// the stamp is cleared. Called wherever presence changes, so longest-idle routing measures idleness rather
    /// than time since the last presence transition of any kind.
    /// </summary>
    /// <param name="profile">The profile whose presence has just been set.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    public static void ApplyIdleState(AgentProfile profile, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var isIdle = profile.PresenceStatus == AgentPresenceStatus.Available &&
            string.IsNullOrEmpty(profile.ActiveReservationId);

        if (!isIdle)
        {
            profile.IdleSinceUtc = null;

            return;
        }

        // Already idle: keep the original instant, or the agent would appear freshly idle on every save.
        profile.IdleSinceUtc ??= nowUtc;
    }
}
