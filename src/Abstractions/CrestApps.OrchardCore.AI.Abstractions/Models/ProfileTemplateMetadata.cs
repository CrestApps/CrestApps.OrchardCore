namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Metadata for AI templates with a "Profile" source.
/// Stored in the template's <see cref="OrchardCore.Entities.Entity.Properties"/> via
/// <c>Put&lt;ProfileTemplateMetadata&gt;</c> / <c>As&lt;ProfileTemplateMetadata&gt;</c>.
/// </summary>
public sealed class ProfileTemplateMetadata
{
    /// <summary>
    /// Gets or sets the type of AI profile this template creates.
    /// </summary>
    public AIProfileType? ProfileType { get; set; }

    /// <summary>
    /// Gets or sets the connection name to pre-fill.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the name of the orchestrator to use.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the system message for the profile.
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
    /// Gets or sets the agent profile names to associate with the profile.
    /// </summary>
    public string[] AgentNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the description of the profile's capabilities.
    /// Used for <see cref="AIProfileType.Agent"/> templates to describe
    /// what the agent does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the availability mode for agent profiles.
    /// Controls whether the agent is always included in every request
    /// or only when matched by relevance scoring.
    /// </summary>
    public AgentAvailability? AgentAvailability { get; set; }
}
