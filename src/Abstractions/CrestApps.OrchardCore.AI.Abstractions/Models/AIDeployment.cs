using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

public class AIDeployment : SourceCatalogEntry, INameAwareModel, ISourceAwareModel, ICloneable<AIDeployment>
{
    private string _modelName;

    /// <summary>
    /// Gets or sets the technical name of the AI client implementation to use for this deployment.
    /// This maps to a registered key in <c>AIOptions.Clients</c>.
    /// For connection-based deployments, this is typically derived from the connection's <c>ClientName</c>.
    /// </summary>
    public string ClientName
    {
        get => Source;
        set => Source = value;
    }

    [Obsolete("Use ClientName instead. Retained for backward compatibility.")]
    [JsonIgnore]
    public string ProviderName
    {
        get => Source;
        set => Source = value;
    }

    [JsonInclude]
    [JsonPropertyName("ProviderName")]
    private string _providerNameBackingField
    {
        set => Source = value;
    }

    /// <summary>
    /// Gets or sets the unique technical name used to identify this deployment in settings, profiles, and recipes.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the provider-facing model or deployment name.
    /// Falls back to <see cref="Name"/> for backward compatibility with legacy records.
    /// </summary>
    public string ModelName
    {
        get => string.IsNullOrWhiteSpace(_modelName) ? Name : _modelName;
        set => _modelName = value?.Trim();
    }

    public string ConnectionName { get; set; }

    public string ConnectionNameAlias { get; set; }

    /// <summary>
    /// Gets or sets the capability types of this deployment (Chat, Utility, Embedding, Image, SpeechToText, TextToSpeech).
    /// A deployment can support one or more capabilities.
    /// </summary>
    public AIDeploymentType Type { get; set; }

    /// <summary>
    /// Gets or sets whether this deployment is the default for its selected capability types
    /// within its connection.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public bool SupportsType(AIDeploymentType type)
        => Type.Supports(type);

    public AIDeployment Clone()
    {
        return new AIDeployment
        {
            ItemId = ItemId,
            Name = Name,
            ModelName = _modelName,
            Source = Source,
            ConnectionName = ConnectionName,
            ConnectionNameAlias = ConnectionNameAlias,
            Type = Type,
            IsDefault = IsDefault,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
