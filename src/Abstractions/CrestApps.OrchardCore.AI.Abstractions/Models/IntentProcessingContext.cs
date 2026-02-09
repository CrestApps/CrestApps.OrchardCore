using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Context for document processing strategy execution.
/// Contains both input data and the result that strategies can update.
/// </summary>
public sealed class IntentProcessingContext
{
    /// <summary>
    /// Gets or sets the user's prompt text.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the chat interaction containing documents and configuration.
    /// </summary>
    public ChatInteraction Interaction { get; set; }

    /// <summary>
    /// Gets or sets the full documents with text and chunks, loaded on demand.
    /// This is populated when strategies need access to document content.
    /// </summary>
    public IList<ChatInteractionDocument> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the conversation history (past messages) for context.
    /// This allows strategies to reference previous prompts and responses.
    /// </summary>
    public IList<ChatMessage> ConversationHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of past messages to include in context for image generation.
    /// Default is 5 messages.
    /// </summary>
    public int MaxHistoryMessagesForImageGeneration { get; set; } = 5;

    /// <summary>
    /// Gets or sets the cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets the result of intent processing. Multiple strategies can add context to this result.
    /// This also contains detected intent metadata (Intent/Confidence/Reason).
    /// </summary>
    public IntentProcessingResult Result { get; } = new IntentProcessingResult();
}
