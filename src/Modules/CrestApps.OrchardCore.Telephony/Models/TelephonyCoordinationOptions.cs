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

    /// <summary>
    /// Gets or sets how long a caller waits to acquire the per-user OAuth token-refresh lock before giving up.
    /// While one request refreshes a user's tokens, its peers wait here for that refresh to land rather than
    /// starting a competing refresh that would rotate the replacement token out from under it.
    /// </summary>
    public TimeSpan TokenRefreshLockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how long the per-user OAuth token-refresh lock is held before it expires on its own, which
    /// bounds how long a crashed holder blocks its peers. It must comfortably exceed a full refresh critical
    /// section: a provider token-exchange round trip (a provider HTTP client may itself allow up to 30 seconds)
    /// plus settings resolution, token protection, and the durable user commit. The lease does not auto-renew,
    /// so if it expires mid-refresh a peer may acquire it and refresh concurrently.
    /// </summary>
    public TimeSpan TokenRefreshLockExpiration { get; set; } = TimeSpan.FromSeconds(60);
}
