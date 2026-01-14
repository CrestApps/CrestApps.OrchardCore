using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for document processing strategies that handle different user intents.
/// Each strategy implements a specific approach to processing documents based on the detected intent.
/// </summary>
public interface IDocumentProcessingStrategy
{
    /// <summary>
    /// Determines whether this strategy can handle the given intent.
    /// </summary>
    /// <param name="intent">The detected document intent name.</param>
    /// <returns>True if this strategy can handle the intent; otherwise, false.</returns>
    bool CanHandle(string intent);

    /// <summary>
    /// Processes the documents according to the strategy and returns context for the AI.
    /// </summary>
    /// <param name="context">The processing context containing documents and intent.</param>
    /// <returns>The processing result containing additional context for the AI.</returns>
    Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context);
}
