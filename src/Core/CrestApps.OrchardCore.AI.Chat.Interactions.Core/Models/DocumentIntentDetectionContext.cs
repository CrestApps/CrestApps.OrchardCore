using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Context for intent detection, providing the necessary information to classify user intent.
/// </summary>
public sealed class DocumentIntentDetectionContext
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
    /// Gets the list of document info attached to the interaction.
    /// </summary>
    public IList<ChatInteractionDocumentInfo> Documents => Interaction?.Documents ?? [];

    /// <summary>
    /// Gets or sets the cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
