using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Provider that routes document processing through all registered strategies.
/// Each strategy is called in sequence until one handles the request.
/// If no strategy handles it, the fallback strategy is used.
/// </summary>
public interface IDocumentProcessingStrategyProvider
{
    /// <summary>
    /// Processes documents by calling all registered strategies until one handles the request.
    /// If no strategy handles the request, the fallback strategy is used.
    /// </summary>
    /// <param name="context">The processing context containing documents and intent.</param>
    /// <returns>The processing result containing additional context for the AI.</returns>
    Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context);
}
