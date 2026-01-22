using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

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
    /// Gets or sets the cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets the result of intent processing. Multiple strategies can add context to this result.
    /// This also contains detected intent metadata (Intent/Confidence/Reason).
    /// </summary>
    public IntentProcessingResult Result { get; } = new IntentProcessingResult();
}
