using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Provider that routes document processing to the appropriate strategy based on detected intent.
/// </summary>
public interface IDocumentProcessingStrategyProvider
{
    /// <summary>
    /// Gets the appropriate strategy for the given intent.
    /// </summary>
    /// <param name="intent">The detected document intent.</param>
    /// <returns>The strategy to use for processing, or null if no strategy can handle the intent.</returns>
    IDocumentProcessingStrategy GetStrategy(DocumentIntent intent);

    /// <summary>
    /// Processes documents using the appropriate strategy for the detected intent.
    /// </summary>
    /// <param name="context">The processing context containing documents and intent.</param>
    /// <returns>The processing result containing additional context for the AI.</returns>
    Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context);
}
