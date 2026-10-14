namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents the lifecycle status of an exemption certificate.
/// </summary>
public enum ExemptionStatus
{
    /// <summary>
    /// The certificate is pending review and is not yet effective.
    /// </summary>
    Pending,

    /// <summary>
    /// The certificate is active and can be applied.
    /// </summary>
    Active,

    /// <summary>
    /// The certificate has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// The certificate has been revoked.
    /// </summary>
    Revoked,
}
