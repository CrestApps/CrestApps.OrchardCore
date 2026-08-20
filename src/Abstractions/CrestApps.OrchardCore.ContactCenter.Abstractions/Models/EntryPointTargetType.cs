namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies what an inbound entry point routes a dialed number (DID) to.
/// </summary>
public enum EntryPointTargetType
{
    /// <summary>
    /// The call routes to a queue and is offered to an available agent by the queue's routing strategy.
    /// </summary>
    Queue,

    /// <summary>
    /// The call routes directly to a specific agent (a personal line). When that agent is unavailable, the
    /// call falls back to the entry point's target queue for normal routing.
    /// </summary>
    Agent,
}
