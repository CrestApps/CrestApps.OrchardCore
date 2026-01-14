using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for detecting user intent when documents are attached to a chat interaction.
/// Implementations can use AI models, heuristics, or other methods to classify intent.
/// </summary>
public interface IDocumentIntentDetector
{
    /// <summary>
    /// Detects the user's intent based on their prompt and attached documents.
    /// </summary>
    /// <param name="context">The context containing the prompt, documents, and interaction.</param>
    /// <returns>The detected intent result with confidence level.</returns>
    Task<DocumentIntentResult> DetectAsync(DocumentIntentDetectionContext context);
}
