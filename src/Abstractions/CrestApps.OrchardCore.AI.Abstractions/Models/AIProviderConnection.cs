using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Models;

public class AIProviderConnection : Entity
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string ProviderName { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIDeployment Clone()
    {
        return new AIDeployment
        {
            Id = Id,
            Name = Name,
            ProviderName = ProviderName,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
