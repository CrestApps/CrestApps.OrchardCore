using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIProviderConnection : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel
{
    public string Name { get; set; }

    public string DisplayText { get; set; }

    public AIProviderConnectionType Type { get; set; }

    public string DefaultDeploymentName { get; set; }

    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string ProviderName
    {
        get => Source;
        set => Source = value;
    }

    [JsonInclude]
    [JsonPropertyName(nameof(ProviderName))]
    private string _providerNameBackingField
    {
        set => Source = value;
    }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIProviderConnection Clone()
    {
        return new AIProviderConnection
        {
            Id = Id,
            Source = Source,
            Name = Name,
            DisplayText = DisplayText,
            IsDefault = IsDefault,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
