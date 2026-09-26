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
    /// Gets or sets how long an in-progress interaction the client recorded itself -- a browser-originated call, which
    /// has no provider identity the reconciler could look up -- is left alone before it is treated as abandoned.
    /// The client settles these when the call ends; the reconciler only has to catch the case where the browser
    /// went away mid-call and never did. Set well above any plausible call, because removing one of these while it is
    /// live disconnects the agent: the removal is announced to the soft phone as a terminal call state, and the
    /// phone tears the session down. That is exactly what a sweep with no such ceiling did to every keypad call that
    /// outlived the next minute tick.
    /// </summary>
    public TimeSpan ClientRecordedCallMaxAge { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Gets or sets how long an in-progress interaction the client recorded itself may go without the soft phone
    /// reporting it still up before reconciliation settles it, as of the last report heard. The phone reports its calls
    /// every thirty seconds while they last, so this only elapses once the phone is gone -- a page that crashed, an app
    /// closed mid-call -- or its hub connection has been down for this long. The record is settled, not removed, and the
    /// soft phone is not told: a phone that is still on the call keeps it.
    /// </summary>
    public TimeSpan ClientRecordedCallSilenceTimeout { get; set; } = TimeSpan.FromMinutes(5);

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
