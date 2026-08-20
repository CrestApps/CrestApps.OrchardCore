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

        if (isOpen)
        {
            plan.ShouldQueue = true;
            plan.TargetQueueId = entryPoint.TargetQueueId;

            // An agent-target entry point still enqueues into the target queue, but the call is first offered
            // directly to the named agent; the queue then acts as the fallback when that agent is unavailable.
            if (entryPoint.TargetType == EntryPointTargetType.Agent && !string.IsNullOrEmpty(entryPoint.TargetAgentId))
            {
                plan.RouteToAgent = true;
                plan.TargetAgentId = entryPoint.TargetAgentId;
            }

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
}
