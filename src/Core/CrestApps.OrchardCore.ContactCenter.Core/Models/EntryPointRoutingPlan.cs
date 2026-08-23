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
    /// Gets or sets a value indicating whether the call should be offered directly to a specific agent
    /// (<see cref="TargetAgentId"/>) as a personal line. When set, <see cref="TargetQueueId"/> is the synthetic
    /// direct-routing queue that carries the call through the reservation pipeline; the call is never offered to
    /// any other agent.
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
    /// Gets or sets the ring window, in seconds, for a direct-to-agent route: how long the caller rings and is
    /// held waiting for the named agent before being sent to voicemail. Only meaningful when
    /// <see cref="RouteToAgent"/> is <see langword="true"/>.
    /// </summary>
    public int RingTimeoutSeconds { get; set; } = ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

    /// <summary>
    /// Gets or sets the entry point's default (fallback) spoken voicemail greeting, used when the recipient agent
    /// has not set their own.
    /// </summary>
    public string VoicemailGreetingText { get; set; }

    /// <summary>
    /// Gets or sets the action to apply while the entry point is closed.
    /// </summary>
    public EntryPointClosedAction ClosedAction { get; set; }
}
