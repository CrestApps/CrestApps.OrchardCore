namespace CrestApps.OrchardCore.AgenticCommerce.Core.Models;

/// <summary>
/// One of the store's ES256 signing keys.
/// </summary>
public sealed class MerchantSigningKey
{
    /// <summary>
    /// Gets or sets the key's id, which signatures name.
    /// </summary>
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the key pair as a JSON Web Key, protected.
    /// </summary>
    public string ProtectedPrivateJwk { get; set; }

    /// <summary>
    /// Gets or sets the public half as a JSON Web Key.
    /// </summary>
    public string PublicJwk { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it stopped signing, or <see langword="null"/> while it signs.
    /// </summary>
    public DateTime? RetiredUtc { get; set; }
}
