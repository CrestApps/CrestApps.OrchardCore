namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// What the server-side check of a credential found.
/// </summary>
public enum CredentialVerificationOutcome
{
    /// <summary>
    /// No credential has the code.
    /// </summary>
    NotFound,

    /// <summary>
    /// The signature is valid, the credential has not expired and is not revoked.
    /// </summary>
    Valid,

    /// <summary>
    /// The signature is valid but the credential is past its expiry.
    /// </summary>
    Expired,

    /// <summary>
    /// The credential is revoked in its status list.
    /// </summary>
    Revoked,

    /// <summary>
    /// The signature does not verify with a published key.
    /// </summary>
    Invalid,
}
