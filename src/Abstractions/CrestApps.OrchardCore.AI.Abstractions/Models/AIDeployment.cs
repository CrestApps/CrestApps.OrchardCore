using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public class AIDeployment : SourceCatalogEntry, INameAwareModel, ISourceAwareModel
{
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

    public string Name { get; set; }

    public string ConnectionName { get; set; }

    public string ConnectionNameAlias { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIDeployment Clone()
    {
        return new AIDeployment
        {
            Id = Id,
            Name = Name,
            Source = Source,
            ConnectionName = ConnectionName,
            ConnectionNameAlias = ConnectionNameAlias,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
