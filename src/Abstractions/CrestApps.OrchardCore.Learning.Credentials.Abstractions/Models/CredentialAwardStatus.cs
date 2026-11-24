namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// The state of a credential award.
/// </summary>
public enum CredentialAwardStatus
{
    /// <summary>
    /// Valid: not revoked and not past its expiry.
    /// </summary>
    Active,

    /// <summary>
    /// Past its expiry.
    /// </summary>
    Expired,

    /// <summary>
    /// Revoked by staff or by a personal data erasure.
    /// </summary>
    Revoked,
}
