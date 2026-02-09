using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Interface for processing tabular data batches using LLM.
/// Implementations handle splitting documents into batches, executing LLM calls
/// with bounded concurrency, and aggregating results.
/// </summary>
public interface ITabularBatchProcessor
{
    /// <summary>
    /// Splits tabular content into batches based on the configured batch size.
    /// </summary>
    /// <param name="content">The full tabular content (CSV/TSV text with header row).</param>
    /// <param name="fileName">The source file name for reference.</param>
    /// <returns>A list of batches ready for processing.</returns>
    IList<TabularBatch> SplitIntoBatches(string content, string fileName);

    /// <summary>
    /// Processes multiple batches concurrently with the LLM and returns aggregated results.
    /// </summary>
    /// <param name="batches">The batches to process.</param>
    /// <param name="userPrompt">The user's original prompt/instruction.</param>
    /// <param name="context">The processing context containing configuration.</param>
    /// <returns>The aggregated results from all batches, ordered by batch index.</returns>
    Task<IList<TabularBatchResult>> ProcessBatchesAsync(
        IList<TabularBatch> batches,
        string userPrompt,
        IntentProcessingContext context);

    /// <summary>
    /// Merges batch results into a single output string, preserving row order.
    /// </summary>
    /// <param name="results">The batch results to merge.</param>
    /// <param name="includeHeader">Whether to include a header row in the merged output.</param>
    /// <returns>The merged output content.</returns>
    string MergeResults(IList<TabularBatchResult> results, bool includeHeader = true);
}
