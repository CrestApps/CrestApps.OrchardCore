using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// A key a module kept before it moved onto the shared signing keys, imported with its id and dates so what it signed
/// still verifies and it rotates when it would have.
/// </summary>
public sealed class SigningKeyImport
{
    /// <summary>
    /// Gets or sets the key id.
    /// </summary>
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the key pair as a JSON Web Key.
    /// </summary>
    public JsonObject PrivateJwk { get; set; }

    /// <summary>
    /// Gets or sets the public half exactly as it was published.
    /// </summary>
    public JsonObject PublicJwk { get; set; }

    /// <summary>
    /// Gets or sets when the key was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it stopped signing, in UTC, or <see langword="null"/> while it signs.
    /// </summary>
    public DateTime? RetiredUtc { get; set; }
}
