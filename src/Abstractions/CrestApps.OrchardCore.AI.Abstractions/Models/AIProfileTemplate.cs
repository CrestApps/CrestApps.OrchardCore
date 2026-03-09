using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a reusable template for creating AI Profiles.
/// Contains pre-configured values for profile fields, parameters, tools, and data sources.
/// Templates can be stored in the database (via UI) or discovered from markdown files.
/// </summary>
public sealed class AIProfileTemplate : CatalogItem, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProfileTemplate>
{
    /// <summary>
    /// Gets or sets the technical name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text of the template.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description of what this template provides.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category for grouping templates in the UI.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets whether this template appears in listing UIs.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsListable { get; set; } = true;

    /// <summary>
    /// Gets or sets the type of AI profile this template creates.
    /// </summary>
    public AIProfileType? ProfileType { get; set; }

    /// <summary>
    /// Gets or sets the connection name to pre-fill.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the system message for the profile.
    /// For file-based templates, this comes from the markdown body.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the welcome message shown to users.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the template for the prompt.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the subject of the prompt.
    /// </summary>
    public string PromptSubject { get; set; }

    /// <summary>
    /// Gets or sets the type of title used in the session.
    /// </summary>
    public AISessionTitleType? TitleType { get; set; }

    /// <summary>
    /// Gets or sets the name of the orchestrator to use.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the temperature parameter for AI completion.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the TopP parameter for AI completion.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty parameter.
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty parameter.
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens for AI completion.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of past messages to include in context.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets the tool names to associate with the profile.
    /// </summary>
    public string[] ToolNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the template was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owner of this template.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author of this template.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Creates a deep copy of the current template.
    /// </summary>
    public AIProfileTemplate Clone()
    {
        return new AIProfileTemplate
        {
            ItemId = ItemId,
            Name = Name,
            DisplayText = DisplayText,
            Description = Description,
            Category = Category,
            IsListable = IsListable,
            ProfileType = ProfileType,
            ConnectionName = ConnectionName,
            SystemMessage = SystemMessage,
            WelcomeMessage = WelcomeMessage,
            PromptTemplate = PromptTemplate,
            PromptSubject = PromptSubject,
            TitleType = TitleType,
            OrchestratorName = OrchestratorName,
            Temperature = Temperature,
            TopP = TopP,
            FrequencyPenalty = FrequencyPenalty,
            PresencePenalty = PresencePenalty,
            MaxOutputTokens = MaxOutputTokens,
            PastMessagesCount = PastMessagesCount,
            ToolNames = ToolNames != null ? [.. ToolNames] : [],
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties.DeepClone().AsObject(),
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
