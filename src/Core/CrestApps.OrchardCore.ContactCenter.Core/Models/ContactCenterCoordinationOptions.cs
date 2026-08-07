namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The distributed-lock timings the Contact Center coordinates inbound work with. These are deployment
/// characteristics rather than product constants: a node under heavier load, or one further from its database,
/// needs a longer lease before a peer may assume the holder died.
/// </summary>
public sealed class ContactCenterCoordinationOptions
{
    /// <summary>
    /// Gets or sets how long a node waits to acquire the inbound routing lock for a call before giving up. A
    /// caller that gives up does not route the call twice; it defers to the node that holds the lock.
    /// </summary>
    public TimeSpan InboundLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long the inbound routing lock is held before it expires on its own, which bounds how
    /// long a crashed node blocks routing for the same call.
    /// </summary>
    public TimeSpan InboundLockExpiration { get; set; } = TimeSpan.FromMinutes(1);
}
