namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Represents a cached entry for tabular batch processing results.
/// </summary>
public sealed class TabularBatchCacheEntry
{
    /// <summary>
    /// Gets or sets the cached batch processing results.
    /// </summary>
    public IList<TabularBatchResult> Results { get; set; }

    /// <summary>
    /// Gets or sets the merged output content from all batches.
    /// </summary>
    public string MergedOutput { get; set; }

    /// <summary>
    /// Gets or sets the total number of batches processed.
    /// </summary>
    public int TotalBatches { get; set; }

    /// <summary>
    /// Gets or sets the number of successful batches.
    /// </summary>
    public int SuccessfulBatches { get; set; }

    /// <summary>
    /// Gets or sets the total rows processed successfully.
    /// </summary>
    public int TotalRowsProcessed { get; set; }

    /// <summary>
    /// Gets or sets when this entry was created (UTC).
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the prompt that was used to generate these results.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the file names that were processed.
    /// </summary>
    public IList<string> FileNames { get; set; }
}
