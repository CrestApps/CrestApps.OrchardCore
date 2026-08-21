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
                plan.RingTimeoutSeconds = entryPoint.VoicemailEnabled
                    ? ResolveRingTimeout(entryPoint.RingTimeoutSeconds)
                    : 0;
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
