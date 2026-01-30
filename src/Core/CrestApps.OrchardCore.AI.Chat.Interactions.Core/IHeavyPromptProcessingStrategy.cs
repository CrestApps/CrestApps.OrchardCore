namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Marker interface for heavy prompt processing strategies that can make many API calls.
/// Heavy strategies (like row-level tabular analysis) are only executed when
/// <see cref="Models.ChatInteractionOptions.EnableHeavyProcessingStrategies"/> is true.
/// </summary>
/// <remarks>
/// Implement this interface instead of <see cref="IPromptProcessingStrategy"/> for strategies that:
/// - Make multiple LLM API calls per request (e.g., batch processing)
/// - Process large datasets row-by-row
/// - Have significant cost or time implications
/// 
/// These strategies are registered normally but filtered at runtime by the strategy provider.
/// </remarks>
public interface IHeavyPromptProcessingStrategy : IPromptProcessingStrategy
{
}
