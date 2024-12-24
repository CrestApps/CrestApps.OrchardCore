using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class AIChatProfile : Entity
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the query.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets the name of the source for this query.
    /// </summary>
    public string Source { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    public AIChatProfile Clone()
    {
        return new AIChatProfile()
        {
            Id = Id,
            Title = Title,
            Source = Source,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties,
        };
    }
}
