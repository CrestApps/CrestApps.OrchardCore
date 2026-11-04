namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// The state of one person's certificate.
/// </summary>
public enum CertificationStatus
{
    /// <summary>
    /// Entered but not checked yet; it doesn't count.
    /// </summary>
    Pending,

    /// <summary>
    /// Checked; it counts until it expires.
    /// </summary>
    Valid,

    /// <summary>
    /// Past its expiry date.
    /// </summary>
    Expired,

    /// <summary>
    /// Withdrawn before it expired.
    /// </summary>
    Revoked,
}
