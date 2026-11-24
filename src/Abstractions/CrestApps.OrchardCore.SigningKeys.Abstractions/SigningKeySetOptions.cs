namespace CrestApps.OrchardCore.SigningKeys;

/// <summary>
/// A key set a module signs with: its algorithm, how long a key signs before a new one replaces it, and how long a key
/// that stopped signing stays published so what it signed can still be verified.
/// </summary>
public sealed class SigningKeySetOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKeySetOptions"/> class.
    /// </summary>
    /// <param name="name">The stable name of the key set.</param>
    public SigningKeySetOptions(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
    }

    /// <summary>
    /// Gets the stable name of the key set, such as <c>AgenticCommerce.Merchant</c>. It names the key secrets.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the algorithm, one of <see cref="SigningKeyAlgorithms"/>.
    /// </summary>
    public string Algorithm { get; set; } = SigningKeyAlgorithms.RS256;

    /// <summary>
    /// Gets or sets how long a key signs before a new one replaces it.
    /// </summary>
    public TimeSpan RotationAge { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets how long a key that stopped signing stays published. Its secret is removed after four times as long.
    /// </summary>
    public TimeSpan RetiredLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the start of the key ids, such as <c>merchant</c>; the creation date and a random part follow.
    /// </summary>
    public string KeyIdPrefix { get; set; } = "key";

    /// <summary>
    /// Gets or sets the description given to the key secrets in Orchard Core Secrets.
    /// </summary>
    public string Description { get; set; }
}
