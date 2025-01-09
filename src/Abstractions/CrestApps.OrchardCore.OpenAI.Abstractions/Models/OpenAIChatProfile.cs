using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatProfile : Entity
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the query.
    /// </summary>
    public string Name { get; set; }

    public string[] FunctionNames { get; set; }

    public OpenAIChatProfileType Type { get; set; }

    /// <summary>
    /// Gets the name of the source for this query.
    /// </summary>
    public string Source { get; set; }

    public string DeploymentId { get; set; }

    public OpenAISessionTitleType? TitleType { get; set; }

    public string WelcomeMessage { get; set; }

    public string SystemMessage { get; set; }

    public string PromptSubject { get; set; }

    public string PromptTemplate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    public OpenAIChatProfile Clone()
    {
        return new OpenAIChatProfile()
        {
            Id = Id,
            Name = Name,
            Source = Source,
            FunctionNames = FunctionNames,
            Type = Type,
            DeploymentId = DeploymentId,
            TitleType = TitleType,
            WelcomeMessage = WelcomeMessage,
            SystemMessage = SystemMessage,
            PromptSubject = PromptSubject,
            PromptTemplate = PromptTemplate,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties,
        };
    }
}
