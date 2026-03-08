using System.Text.Json.Serialization;
using CrestApps.AI.Json;
using CrestApps.Models;
using CrestApps.Services;

namespace CrestApps.AI.Models;

[JsonConverter(typeof(AIProviderConnectionJsonConverter))]
public sealed class AIProviderConnection : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProviderConnection>
{
    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string ChatDeploymentName { get; set; }

    public string EmbeddingDeploymentName { get; set; }

    public string ImagesDeploymentName { get; set; }

    public string UtilityDeploymentName { get; set; }

    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string ProviderName
    {
        get => Source;
        set => Source = value;
    }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIProviderConnection Clone()
    {
        return new AIProviderConnection
        {
            ItemId = ItemId,
            Source = Source,
            Name = Name,
            DisplayText = DisplayText,
            IsDefault = IsDefault,
            ChatDeploymentName = ChatDeploymentName,
            EmbeddingDeploymentName = EmbeddingDeploymentName,
            ImagesDeploymentName = ImagesDeploymentName,
            UtilityDeploymentName = UtilityDeploymentName,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
