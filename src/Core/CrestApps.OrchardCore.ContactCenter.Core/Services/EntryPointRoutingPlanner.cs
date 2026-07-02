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
