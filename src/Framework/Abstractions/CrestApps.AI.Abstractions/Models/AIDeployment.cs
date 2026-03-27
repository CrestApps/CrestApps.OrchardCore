using System.Text.Json.Serialization;
using CrestApps.Models;
using CrestApps.Services;

namespace CrestApps.AI.Models;

public class AIDeployment : SourceCatalogEntry, INameAwareModel, ISourceAwareModel, ICloneable<AIDeployment>
{
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

    public string Name { get; set; }

    public string ConnectionName { get; set; }

    public string ConnectionNameAlias { get; set; }

    /// <summary>
    /// Gets or sets the type of this deployment (Chat, Utility, Embedding, Image, SpeechToText, TextToSpeech).
    /// Determines what capability this deployment provides.
    /// </summary>
    public AIDeploymentType Type { get; set; }

    /// <summary>
    /// Gets or sets whether this deployment is the default for its <see cref="Type"/>
    /// within its connection. Each connection can have at most one default per type.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIDeployment Clone()
    {
        return new AIDeployment
        {
            ItemId = ItemId,
            Name = Name,
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
