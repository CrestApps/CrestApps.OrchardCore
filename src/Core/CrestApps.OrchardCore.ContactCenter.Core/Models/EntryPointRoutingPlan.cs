using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the routing decision derived from an inbound entry point and its business-hours state.
/// </summary>
public sealed class EntryPointRoutingPlan
{
    /// <summary>
    /// Gets or sets the matched entry point.
    /// </summary>
    public ContactCenterEntryPoint EntryPoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry point is currently open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call should be enqueued.
    /// </summary>
    public bool ShouldQueue { get; set; }

    /// <summary>
    /// Gets or sets the effective queue identifier the call should be enqueued into.
    /// </summary>
    public string TargetQueueId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queued call should first be offered directly to a
    /// specific agent (<see cref="TargetAgentId"/>) before the queue's normal routing takes over.
    /// </summary>
    public bool RouteToAgent { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile the call should be offered to directly when
    /// <see cref="RouteToAgent"/> is <see langword="true"/>.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the priority to assign to the queued call.
    /// </summary>
    public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

    /// <summary>
    /// Gets or sets the action to apply while the entry point is closed.
    /// </summary>
    public EntryPointClosedAction ClosedAction { get; set; }
}
