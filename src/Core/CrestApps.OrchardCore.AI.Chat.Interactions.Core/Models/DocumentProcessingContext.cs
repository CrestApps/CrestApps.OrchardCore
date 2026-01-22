using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Context for document processing strategy execution.
/// Contains both input data and the result that strategies can update.
/// </summary>
public sealed class DocumentProcessingContext
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
    /// Gets the list of documents attached to the interaction.
    /// </summary>
    public IList<ChatInteractionDocument> Documents => Interaction?.Documents ?? [];

    /// <summary>
    /// Gets or sets the detected intent for this processing context.
    /// </summary>
    public DocumentIntentResult IntentResult { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets or sets the service provider for resolving dependencies.
    /// </summary>
    public IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Gets the result of document processing. Multiple strategies can add context to this result.
    /// </summary>
    public DocumentProcessingResult Result { get; } = new DocumentProcessingResult();
}
