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
    /// Keeps <see cref="AgentProfile.IdleSinceUtc"/> in step with a state transition, so longest-idle routing ranks
    /// agents by how long they have waited for work rather than by when their presence last changed.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Staying Available keeps the original instant, or the agent would look freshly idle on every save.</item>
    /// <item>Going from Available into an offer or a call (Reserved or Busy) holds the instant: routing never ranks an
    /// agent who is not Available, and holding it is what lets <paramref name="keepsIdlePlace"/> give it back.</item>
    /// <item>Returning to Available from an offer or a call starts a fresh clock at <paramref name="changedUtc"/>,
    /// sending the agent to the back of the line, unless <paramref name="keepsIdlePlace"/> says the work never reached
    /// them, in which case the held instant is kept.</item>
    /// <item>Any other state (wrap-up, a break, offline) is not idle and clears the stamp.</item>
    /// </list>
    /// Before this ran on every transition, only sign-in, sign-out, presence changes, wrap-up and completing work
    /// touched the stamp, and only with "keep if set". An agent who went Reserved and Busy and then straight back to
    /// Available -- a call ended without wrap-up, an agent leg that failed, a disposition saved while still on the
    /// call -- kept the stamp from before the work, stayed first in line and took call after call.
    /// </remarks>
    /// <param name="profile">The profile whose presence has just been set.</param>
    /// <param name="previousState">The state the agent was in before the transition.</param>
    /// <param name="changedUtc">When the transition took effect.</param>
    /// <param name="keepsIdlePlace">Whether a return to Available keeps the agent's held place in the line.</param>
    public static void ApplyIdleState(
        AgentProfile profile,
        AgentPresenceStatus previousState,
        DateTime changedUtc,
        bool keepsIdlePlace)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var wasIdleOrWorking = previousState is AgentPresenceStatus.Available or AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy;

        if (profile.PresenceStatus == AgentPresenceStatus.Available && string.IsNullOrEmpty(profile.ActiveReservationId))
        {
            if (previousState == AgentPresenceStatus.Available || (keepsIdlePlace && wasIdleOrWorking))
            {
                profile.IdleSinceUtc ??= changedUtc;
            }
            else
            {
                profile.IdleSinceUtc = changedUtc;
            }

            return;
        }

        if (profile.PresenceStatus is AgentPresenceStatus.Available or AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy &&
            wasIdleOrWorking)
        {
            return;
        }

        profile.IdleSinceUtc = null;
    }
}
