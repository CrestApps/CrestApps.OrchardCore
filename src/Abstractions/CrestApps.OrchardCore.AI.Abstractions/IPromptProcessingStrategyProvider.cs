using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provider that routes document processing through all registered strategies.
/// All strategies are called in sequence, allowing multiple strategies to contribute context.
/// </summary>
public interface IPromptProcessingStrategyProvider
{
    /// <summary>
    /// Processes documents by calling all registered strategies.
    /// Each strategy can add context to the result, allowing multiple strategies to contribute.
    /// </summary>
    /// <param name="context">The processing context containing documents, intent, and result to update.</param>
    Task ProcessAsync(IntentProcessingContext context);
}
