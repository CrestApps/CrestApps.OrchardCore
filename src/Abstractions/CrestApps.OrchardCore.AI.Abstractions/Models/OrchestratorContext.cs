using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Provides the input context for an <see cref="IOrchestrator"/> execution.
/// Contains the user message, conversation history, completion settings,
/// document references, and extensible properties populated by
/// <see cref="IOrchestrationContextHandler"/> implementations.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="IOrchestrationContextBuilder"/> using a handler pipeline,
/// allowing modules to contribute additional context (e.g., document metadata, MCP connections,
/// data sources) without modifying the core orchestrator.
/// </remarks>
public sealed class OrchestrationContext
{
    /// <summary>
    /// Gets or sets the current user message to process.
    /// </summary>
    public string UserMessage { get; set; }

    /// <summary>
    /// Gets or sets the conversation history (prior messages in the session).
    /// </summary>
    public IList<ChatMessage> ConversationHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the completion context containing tool names, system message,
    /// model parameters, and other configuration built from the profile or interaction.
    /// </summary>
    public AICompletionContext CompletionContext { get; set; }

    /// <summary>
    /// Gets or sets the source/provider name for the completion service
    /// (e.g., the AI client implementation name).
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the document references available for this session.
    /// System tools use these to load document content on demand.
    /// </summary>
    public IList<ChatInteractionDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets the extensible property bag for additional context contributed by
    /// <see cref="IOrchestrationContextHandler"/> implementations.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
