namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// The distributed-lock timings the Telephony module coordinates with. These are deployment characteristics
/// rather than product constants: a node under heavier load, or one further from its database, needs a longer
/// lease before a peer may assume the holder died.
/// </summary>
public sealed class TelephonyCoordinationOptions
{
    /// <summary>
    /// Gets or sets how long a caller waits to acquire the interaction synchronization lock before giving up.
    /// </summary>
    public TimeSpan InteractionLockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long the interaction synchronization lock is held before it expires on its own, which
    /// bounds how long a crashed holder blocks its peers.
    /// </summary>
    public TimeSpan InteractionLockExpiration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how long a newly created interaction is protected from reconciliation, so a record that
    /// another node has just written is not treated as orphaned before that write is visible.
    /// </summary>
    public TimeSpan NewInteractionGracePeriod { get; set; } = TimeSpan.FromSeconds(15);
}
