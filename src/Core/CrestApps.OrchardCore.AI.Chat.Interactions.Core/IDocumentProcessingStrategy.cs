using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for document processing strategies that handle different user intents.
/// Each strategy implements a specific approach to processing documents based on the detected intent.
/// Multiple strategies can contribute to the same processing context by adding context to the result.
/// </summary>
public interface IDocumentProcessingStrategy
{
    /// <summary>
    /// Processes the documents according to the strategy and updates the context result.
    /// If this strategy handles the given intent, it should add context to <see cref="IntentProcessingContext.Result"/>.
    /// If this strategy does not handle the intent, it should return without modifying the result.
    /// </summary>
    /// <param name="context">The processing context containing documents, intent, and result to update.</param>
    Task ProcessAsync(IntentProcessingContext context);
}
