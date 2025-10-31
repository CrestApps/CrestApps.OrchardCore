using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIProfile : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProfile>
{
    /// <summary>
    /// Gets or sets the technical name of the profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text of the profile.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the type of AI chat profile.
    /// </summary>
    public AIProfileType Type { get; set; }

    /// <summary>
    /// Gets or sets the connection name to use for this profile.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment identifier associated with the profile.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the type of title used in the session.
    /// </summary>
    public AISessionTitleType? TitleType { get; set; }

    /// <summary>
    /// Gets or sets the welcome message shown to users.
    /// </summary>
    public string WelcomeMessage { get; set; }

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
    /// <returns>A cloned instance of <see cref="AIProfile"/>.</returns>
    public AIProfile Clone()
    {
        return new AIProfile()
        {
            Id = Id,
            Name = Name,
            DisplayText = DisplayText,
            Source = Source,
            Type = Type,
            DeploymentId = DeploymentId,
            ConnectionName = ConnectionName,
            TitleType = TitleType,
            WelcomeMessage = WelcomeMessage,
            PromptSubject = PromptSubject,
            PromptTemplate = PromptTemplate,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties.Clone(),
            Settings = Settings.Clone(),
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(DisplayText))
        {
            return Name;
        }

        return DisplayText;
    }
}
