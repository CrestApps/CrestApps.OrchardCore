using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Interface for detecting user intent when documents are attached to a chat interaction.
/// Implementations can use AI models, heuristics, or other methods to classify intent.
/// </summary>
public interface IPromptIntentDetector
{
    /// <summary>
    /// Detects the user's intent based on their prompt and attached documents.
    /// </summary>
    /// <param name="context">The context containing the prompt, documents, and routing information.</param>
    /// <returns>The detected intent metadata.</returns>
    Task<DocumentIntent> DetectAsync(PromptRoutingContext context, CancellationToken cancellationToken = default);
}
