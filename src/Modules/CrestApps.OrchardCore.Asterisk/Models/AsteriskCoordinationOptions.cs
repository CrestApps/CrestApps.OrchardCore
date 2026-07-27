namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// The Asterisk timings a deployment tunes rather than the product fixing. The credential lock protects the
/// PJSIP realtime table from concurrent issuance for the same endpoint, and the reclamation threshold decides
/// how long an inbound call may sit unclaimed before another node takes it over.
/// </summary>
public sealed class AsteriskCoordinationOptions
{
    /// <summary>
    /// Gets or sets how long a caller waits to acquire the PJSIP credential issuance lock before giving up.
    /// </summary>
    public TimeSpan CredentialLockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long the PJSIP credential issuance lock is held before it expires on its own, which
    /// bounds how long a crashed node blocks issuance for the same endpoint.
    /// </summary>
    public TimeSpan CredentialLockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long an inbound call may remain pending before reconciliation reclaims it. Setting this
    /// below the longest expected routing delay causes a call to be reclaimed while it is still being answered.
    /// </summary>
    public TimeSpan PendingReclamationThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the ceiling on a single ARI request including retries.
    /// </summary>
    public TimeSpan HttpTotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the ceiling on one ARI request attempt.
    /// </summary>
    public TimeSpan HttpAttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
