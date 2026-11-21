using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// A key of a key set with its private half, as JSON Web Keys.
/// </summary>
public sealed class SigningKey
{
    /// <summary>
    /// Gets or sets the key id, which signatures name in their <c>kid</c> header.
    /// </summary>
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the algorithm, one of <see cref="SigningKeyAlgorithms"/>.
    /// </summary>
    public string Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the key pair as a JSON Web Key, with <c>kid</c>, <c>use</c> and <c>alg</c>.
    /// </summary>
    public JsonObject PrivateJwk { get; set; }

    /// <summary>
    /// Gets or sets the public half as a JSON Web Key, as it is published.
    /// </summary>
    public JsonObject PublicJwk { get; set; }

    /// <summary>
    /// Gets or sets when the key was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
