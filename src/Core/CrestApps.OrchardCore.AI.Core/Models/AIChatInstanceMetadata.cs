namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Metadata for custom AI chat instances that store user-configured settings.
/// This allows users to create chat instances with custom configurations without requiring predefined AI Profiles.
/// </summary>
public sealed class AIChatInstanceMetadata
{
    /// <summary>
    /// Gets or sets the connection name to use for this instance.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment identifier for this instance.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the system message/instructions for the AI.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens in the response.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the temperature parameter for response randomness.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the Top P parameter for token selection.
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
    /// Gets or sets the number of past messages to include in context.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets whether caching should be used for this instance.
    /// </summary>
    public bool UseCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the selected tool names for function invocation.
    /// </summary>
    public string[] ToolNames { get; set; }

    /// <summary>
    /// Gets or sets whether this is a custom configured instance (not using a predefined profile).
    /// </summary>
    public bool IsCustomInstance { get; set; }

    /// <summary>
    /// Gets or sets the provider name associated with the connection.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the source/implementation name (e.g., "OpenAI", "AzureOpenAI", "Ollama").
    /// </summary>
    public string Source { get; set; }
}
