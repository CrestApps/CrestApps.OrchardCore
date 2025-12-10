using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// Represents a custom AI chat instance with user-defined configuration.
/// </summary>
public sealed class AICustomChatInstance : SourceCatalogEntry, IDisplayTextAwareModel
{
    /// <summary>
    /// Gets or sets the display text (title) of the custom chat instance.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the connection name to use for this instance.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment identifier for this instance.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the system instructions for the AI.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens for the response.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the temperature setting for randomness.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the Top P setting for nucleus sampling.
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
    /// Gets or sets the number of past messages to include.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets the selected tool names for this instance.
    /// </summary>
    public IList<string> ToolNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the user identifier who owns this instance.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the instance was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    public override string ToString()
    {
        return DisplayText ?? ItemId;
    }
}
