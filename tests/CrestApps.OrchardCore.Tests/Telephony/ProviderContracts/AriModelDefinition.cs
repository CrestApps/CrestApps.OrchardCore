namespace CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

/// <summary>
/// Represents a single Swagger 1.2 model declared by the Asterisk REST Interface specification.
/// </summary>
internal sealed class AriModelDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AriModelDefinition"/> class.
    /// </summary>
    /// <param name="id">The model identifier as declared by the specification.</param>
    public AriModelDefinition(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the model identifier as declared by the specification.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the model identifier this model inherits from, or <see langword="null"/> when the model is a root model.
    /// </summary>
    public string BaseModelId { get; set; }

    /// <summary>
    /// Gets the property names declared directly on this model mapped to their declared specification type.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}
