using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class ModelDeployment : Entity
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Source { get; set; }

    public string ConnectionName { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public ModelDeployment Clone()
    {
        return new ModelDeployment
        {
            Id = Id,
            Name = Name,
            Source = Source,
            ConnectionName = ConnectionName,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };
    }
}
