using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds an <see cref="EntryPointRoutingPlan"/> from an entry point and its open/closed state.
/// </summary>
public static class EntryPointRoutingPlanner
{
    /// <summary>
    /// Creates the routing plan for the supplied entry point.
    /// </summary>
    /// <param name="entryPoint">The matched entry point.</param>
    /// <param name="isOpen">Whether the entry point is currently open.</param>
    /// <returns>The routing plan.</returns>
    public static EntryPointRoutingPlan CreatePlan(ContactCenterEntryPoint entryPoint, bool isOpen)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        var plan = new EntryPointRoutingPlan
        {
            EntryPoint = entryPoint,
            IsOpen = isOpen,
            Priority = entryPoint.Priority,
            ClosedAction = entryPoint.ClosedAction,
            // The entry point's default greeting applies to every voicemail from this door (agent or queue route),
            // as the fallback when the recipient agent has no greeting of their own.
            VoicemailGreetingText = entryPoint.VoicemailGreetingText,
        };

        var isAgentTarget = entryPoint.TargetType == EntryPointTargetType.Agent &&
            !string.IsNullOrEmpty(entryPoint.TargetAgentId);

        if (isOpen)
        {
            plan.ShouldQueue = true;

            if (isAgentTarget)
            {
                // A specific-agent entry point rings the named agent directly (a personal line): there is no
                // target queue and no queue fallback. The call is carried through the reservation and offer
                // pipeline under the synthetic direct-routing queue so no other agent is ever offered it.
                plan.RouteToAgent = true;
                plan.TargetAgentId = entryPoint.TargetAgentId;
                plan.TargetQueueId = ContactCenterConstants.DirectRouting.QueueId;

                // A ring window of 0 disables voicemail downstream: the caller keeps ringing and is held for the
                // agent. Voicemail on → the configured (or default) window; voicemail off → 0.
                plan.RingTimeoutSeconds = ResolveDirectRingTimeout(entryPoint);
            }
            else
            {
                plan.TargetQueueId = entryPoint.TargetQueueId;
            }

            return plan;
        }

        if (isAgentTarget)
        {
            // A personal line has no queue to hold or overflow into while closed, so the caller is sent to the
            // agent's voicemail unless the entry point is explicitly configured to reject closed calls.
            plan.ShouldQueue = false;
            plan.TargetQueueId = null;
            plan.ClosedAction = entryPoint.ClosedAction == EntryPointClosedAction.Reject
                ? EntryPointClosedAction.Reject
                : EntryPointClosedAction.Voicemail;

            return plan;
        }

        switch (entryPoint.ClosedAction)
        {
            case EntryPointClosedAction.HoldInQueue:
                plan.ShouldQueue = true;
                plan.TargetQueueId = entryPoint.TargetQueueId;
                break;
            case EntryPointClosedAction.Overflow:
                plan.ShouldQueue = true;
                plan.TargetQueueId = string.IsNullOrEmpty(entryPoint.OverflowQueueId)
                    ? entryPoint.TargetQueueId
                    : entryPoint.OverflowQueueId;
                break;
            case EntryPointClosedAction.Voicemail:
            case EntryPointClosedAction.Reject:
                plan.ShouldQueue = false;
                plan.TargetQueueId = null;
                break;
        }

        return plan;
    }

    /// <summary>
    /// Gets how long a call this entry point sends straight to one agent rings before it goes to voicemail: the
    /// configured (or default) window when voicemail is on, or 0 when it is off and the caller is held for the agent.
    /// A phone-menu choice that rings one agent follows the same rule as a personal line.
    /// </summary>
    /// <param name="entryPoint">The entry point.</param>
    /// <returns>The ring window in seconds, or 0 when voicemail is disabled.</returns>
    public static int ResolveDirectRingTimeout(ContactCenterEntryPoint entryPoint)
    {
        ArgumentNullException.ThrowIfNull(entryPoint);

        return entryPoint.VoicemailEnabled
            ? ResolveRingTimeout(entryPoint.RingTimeoutSeconds)
            : 0;
    }

    private static int ResolveRingTimeout(int ringTimeoutSeconds)
    {
        // When voicemail is enabled, a non-positive stored window falls back to the default; otherwise clamp to
        // the supported range.
        if (ringTimeoutSeconds <= 0)
        {
            return ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;
        }

        return Math.Clamp(
            ringTimeoutSeconds,
            ContactCenterConstants.DirectRouting.MinimumRingTimeoutSeconds,
            ContactCenterConstants.DirectRouting.MaximumRingTimeoutSeconds);
    }
}
