namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the fenced, exclusive lease a worker acquires over a <see cref="ProviderCommand"/>. The claim
/// must be presented to perform ownership-guarded transitions such as sending the command or reporting its
/// outcome.
/// </summary>
public sealed class ProviderCommandClaim
{
    /// <summary>
    /// Gets or sets the stable idempotency key of the claimed command.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets the fence token granted for this claim. It is rejected once a newer claim supersedes it.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the opaque owner token identifying the worker that holds the lease.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the lease expires.
    /// </summary>
    public DateTime LeaseExpiresUtc { get; set; }
}
