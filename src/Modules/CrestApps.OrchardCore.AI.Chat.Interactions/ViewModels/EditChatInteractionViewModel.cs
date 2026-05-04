namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction.
/// </summary>
public class EditChatInteractionViewModel
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment name.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the system message.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the top p.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty.
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty.
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the max tokens.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the past messages count.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets the tool names.
    /// </summary>
    public string[] ToolNames { get; set; }

    /// <summary>
    /// Gets or sets the mcp connection ids.
    /// </summary>
    public string[] McpConnectionIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    public bool IsNew { get; set; }
}
