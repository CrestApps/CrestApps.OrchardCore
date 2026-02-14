namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Ambient context that provides AI tool implementations with
/// the provider, connection, and resource information of the current request.
/// Stored in <c>HttpContext.Items</c> by the caller (e.g., AIChatHub).
/// </summary>
public sealed class AIToolExecutionContext
{
    /// <summary>
    /// Gets or sets the name of the AI provider handling the current request
    /// (e.g., "OpenAI", "AzureOpenAI", "Ollama").
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the connection name within the provider.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets the resource (e.g., <see cref="Models.AIProfile"/> or
    /// <see cref="Models.ChatInteraction"/>) that initiated the current AI request.
    /// Tools can cast this to access resource-specific data such as interaction IDs.
    /// </summary>
    public object Resource { get; }

    public AIToolExecutionContext(object resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        Resource = resource;
    }
}
