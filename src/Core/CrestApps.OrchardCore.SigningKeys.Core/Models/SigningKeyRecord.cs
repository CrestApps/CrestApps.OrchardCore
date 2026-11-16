namespace CrestApps.OrchardCore.SigningKeys.Core.Models;

/// <summary>
/// One key of a key set: its id, the secret that holds the key pair, its public half and its dates. The key pair itself
/// is never in this record.
/// </summary>
public sealed class SigningKeyRecord
{
    /// <summary>
    /// Gets or sets the key id.
    /// </summary>
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the algorithm.
    /// </summary>
    public string Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the name of the secret of Orchard Core Secrets that holds the key pair.
    /// </summary>
    public string SecretName { get; set; }

    /// <summary>
    /// Gets or sets the public half as a JSON Web Key, as it is published.
    /// </summary>
    public string PublicJwk { get; set; }

    /// <summary>
    /// Gets or sets when the key was made, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it stopped signing, in UTC, or <see langword="null"/> while it signs.
    /// </summary>
    public DateTime? RetiredUtc { get; set; }
}
