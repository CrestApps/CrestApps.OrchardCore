namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The site's VAPID key pair for web push, with the private key protected.
/// </summary>
internal sealed class WorkforcePushKeys
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the public key, base64url-encoded.
    /// </summary>
    public string PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the private key, protected with data protection.
    /// </summary>
    public string ProtectedPrivateKey { get; set; }
}
