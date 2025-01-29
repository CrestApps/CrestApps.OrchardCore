using System.Text.Json.Nodes;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatProfile : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the profile.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the query.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the name of the source for this query.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the type of OpenAI chat profile.
    /// </summary>
    public OpenAIChatProfileType Type { get; set; }

    /// <summary>
    /// Gets or sets the deployment identifier associated with the profile.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the names of functions associated with this profile.
    /// </summary>
    public string[] FunctionNames { get; set; }

    /// <summary>
    /// Gets or sets the type of title used in the session.
    /// </summary>
    public OpenAISessionTitleType? TitleType { get; set; }

    /// <summary>
    /// Gets or sets the welcome message shown to users.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the system message used in the chat.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the subject of the prompt.
    /// </summary>
    public string PromptSubject { get; set; }

    /// <summary>
    /// Gets or sets the template for the prompt.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owner of this profile.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author of this profile.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the JSON-based settings for the profile.
    /// </summary>
    public JsonObject Settings { get; init; } = [];

    /// <summary>
    /// Creates a deep copy of the current profile.
    /// </summary>
    /// <returns>A cloned instance of <see cref="OpenAIChatProfile"/>.</returns>
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
            Properties = Properties.Clone(),
            Settings = Settings.Clone(),
        };
    }
}
