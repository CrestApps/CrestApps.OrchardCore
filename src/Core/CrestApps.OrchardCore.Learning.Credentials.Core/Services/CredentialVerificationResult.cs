using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The result of the server-side check of a credential.
/// </summary>
public sealed class CredentialVerificationResult
{
    /// <summary>
    /// Gets or sets the award, when one has the code.
    /// </summary>
    public CredentialAward Award { get; set; }

    /// <summary>
    /// Gets or sets what the check found.
    /// </summary>
    public CredentialVerificationOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets whether the JSON Web Token verifies with a published key of the credentials key set.
    /// </summary>
    public bool SignatureValid { get; set; }

    /// <summary>
    /// Gets or sets whether the credential is past its expiry.
    /// </summary>
    public bool Expired { get; set; }

    /// <summary>
    /// Gets or sets whether the status list says the credential is revoked.
    /// </summary>
    public bool Revoked { get; set; }

    /// <summary>
    /// Gets or sets whether the recipient's personal data was erased, so no signed credential is left.
    /// </summary>
    public bool Erased { get; set; }

    /// <summary>
    /// Gets or sets when the credential stops being valid, in UTC, read from the signed token.
    /// </summary>
    public DateTime? ValidUntilUtc { get; set; }
}
