namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Names each reason the availability gate keeps an agent out of a queue's routing. The values are flags, so every
/// reason that applies at once can be reported together; <see cref="None"/> means the gate lets the agent through.
/// </summary>
[Flags]
internal enum AgentRoutingIneligibility
{
    /// <summary>
    /// Nothing keeps the agent out of the queue's routing.
    /// </summary>
    None = 0,

    /// <summary>
    /// The agent's presence is not Available.
    /// </summary>
    NotAvailable = 1,

    /// <summary>
    /// A manager has not entitled the agent to the queue.
    /// </summary>
    NotEntitled = 2,

    /// <summary>
    /// The agent is not signed in to the queue.
    /// </summary>
    NotSignedIn = 4,

    /// <summary>
    /// The agent has no live session: none at all, or one that is offline, has no connection, or has missed its
    /// heartbeat.
    /// </summary>
    NoLiveSession = 8,

    /// <summary>
    /// The agent's live session does not carry the queue, so the workspace the agent has open is not signed in to it.
    /// </summary>
    SessionNotSignedIn = 16,

    /// <summary>
    /// The agent already handles as many interactions as they may handle at once.
    /// </summary>
    AtCapacity = 32,
}
