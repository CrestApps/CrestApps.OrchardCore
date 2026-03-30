using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a chat interaction which combines AI profile configuration and chat session state.
/// This enables ad-hoc creation and execution of chat profiles without predefined AI Profiles.
/// </summary>
public sealed class ChatInteraction : CatalogItem
{
    /// <summary>
    /// Gets or sets the title of the chat interaction.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who owns this interaction.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier who owns this interaction.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment identifier (AI model) to use.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the utility deployment identifier for this interaction.
    /// When not set, falls back to the global default utility deployment.
    /// </summary>
    public string UtilityDeploymentName { get; set; }

    [JsonIgnore]
    [Obsolete("Use ChatDeploymentName instead. Retained for backward compatibility.")]
    public string ChatDeploymentId
    {
        get => ChatDeploymentName;
        set => ChatDeploymentName = value;
    }

    [JsonIgnore]
    [Obsolete("Use UtilityDeploymentName instead. Retained for backward compatibility.")]
    public string UtilityDeploymentId
    {
        get => UtilityDeploymentName;
        set => UtilityDeploymentName = value;
    }

    [Obsolete("Use ChatDeploymentName instead. Retained for backward compatibility.")]
    [JsonIgnore]
    public string DeploymentId
    {
        get => ChatDeploymentName;
        set => ChatDeploymentName = value;
    }

    [JsonInclude]
    [JsonPropertyName("DeploymentId")]
    private string _deploymentIdBackingField
    {
        set => ChatDeploymentName = value;
    }

    [JsonInclude]
    [JsonPropertyName("ChatDeploymentId")]
    private string _chatDeploymentIdBackingField
    {
        set => ChatDeploymentName = value;
    }

    [JsonInclude]
    [JsonPropertyName("UtilityDeploymentId")]
    private string _utilityDeploymentIdBackingField
    {
        set => UtilityDeploymentName = value;
    }

    /// <summary>
    /// Gets or sets the connection name for the AI provider.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the system message/prompt for the AI.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the temperature parameter for AI responses.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the TopP parameter for AI responses.
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
    /// Gets or sets the maximum number of tokens for AI responses.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of past messages to include in context.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets the number of top matching document chunks to include in AI context.
    /// Default is 3 if not specified.
    /// </summary>
    public int? DocumentTopN { get; set; }

    /// <summary>
    /// Gets or sets the name of the orchestrator to use for this interaction.
    /// When <see langword="null"/> or empty, the system default orchestrator is used.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the <see cref="IChatResponseHandler"/> currently
    /// handling prompts for this interaction. When <see langword="null"/> or empty, the default
    /// AI handler is used. This value can be changed mid-conversation (e.g., by an AI
    /// function that transfers the chat to a live-agent platform).
    /// </summary>
    public string ResponseHandlerName { get; set; }

    /// <summary>
    /// Gets or sets the list of AI tool names to use.
    /// </summary>
    public IList<string> ToolNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of agent profile names to include.
    /// Agents are AI profiles with <see cref="AIProfileType.Agent"/> type
    /// that are dynamically exposed as tools for multi-agent orchestration.
    /// </summary>
    public IList<string> AgentNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of MCP connection IDs to use.
    /// </summary>
    public IList<string> McpConnectionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of A2A connection IDs to use.
    /// </summary>
    public IList<string> A2AConnectionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of attached documents for "chat against own data" functionality.
    /// Only applicable when Source is AzureOpenAIOwnData.
    /// </summary>
    public List<ChatDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC date and time when the interaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the last document index.
    /// </summary>
    public int DocumentIndex { get; set; }
}
