namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// How a credential names its recipient: an Open Badges 3.0 identity object with a salted SHA-256 hash of the e-mail
/// address. The plain address is never stored in the award or the credential.
/// </summary>
public sealed class CredentialRecipientIdentity
{
    /// <summary>
    /// Gets or sets the identity type, <c>emailAddress</c>.
    /// </summary>
    public string IdentityType { get; set; }

    /// <summary>
    /// Gets or sets the hash: <c>sha256$</c> and the lowercase hexadecimal SHA-256 of the address followed by the salt.
    /// </summary>
    public string IdentityHash { get; set; }

    /// <summary>
    /// Gets or sets the salt.
    /// </summary>
    public string Salt { get; set; }
}
