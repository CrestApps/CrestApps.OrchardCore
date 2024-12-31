using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class AIChatProfile : Entity
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the query.
    /// </summary>
    public string Name { get; set; }

    public string[] FunctionNames { get; set; }

    /// <summary>
    /// Gets the name of the source for this query.
    /// </summary>
    public string Source { get; set; }

    public string DeploymentId { get; set; }

    public SessionTitleType TitleType { get; set; }

    public string WelcomeMessage { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    public AIChatProfile Clone()
    {
        return new AIChatProfile()
        {
            Id = Id,
            Name = Name,
            Source = Source,
            DeploymentId = DeploymentId,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties,
        };
    }

    public enum SessionTitleType
    {
        InitialPrompt,
        Generated,
    }
}
