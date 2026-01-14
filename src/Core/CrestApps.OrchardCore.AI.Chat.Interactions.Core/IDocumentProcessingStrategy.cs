using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for document processing strategies that handle different user intents.
/// Each strategy implements a specific approach to processing documents based on the detected intent.
/// Strategies are called in sequence and can indicate they did not handle the request by returning
/// <see cref="DocumentProcessingResult.NotHandled"/>, allowing the next strategy to be tried.
/// </summary>
public interface IDocumentProcessingStrategy
{
    /// <summary>
    /// Processes the documents according to the strategy and returns context for the AI.
    /// If this strategy does not handle the given intent, return <see cref="DocumentProcessingResult.NotHandled"/>.
    /// </summary>
    /// <param name="context">The processing context containing documents and intent.</param>
    /// <returns>The processing result containing additional context for the AI, or NotHandled if this strategy does not process the intent.</returns>
    Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context);
}
