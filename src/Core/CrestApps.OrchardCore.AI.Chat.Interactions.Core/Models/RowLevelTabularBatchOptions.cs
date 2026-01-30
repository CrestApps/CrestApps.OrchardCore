namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Configuration settings for row-level tabular data batch processing.
/// These settings control how large tabular datasets are split into batches
/// for LLM processing to avoid token limits and improve reliability.
/// </summary>
public sealed class RowLevelTabularBatchOptions
{
    /// <summary>
    /// Gets or sets the number of data rows per batch (excluding header).
    /// Each batch will contain the header row plus this many data rows.
    /// Default is 25 rows per batch.
    /// </summary>
    /// <remarks>
    /// Lower values reduce token usage per request but increase the number of LLM calls.
    /// Higher values may hit token limits for rows with large content (e.g., transcript columns).
    /// </remarks>
    public int RowBatchSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets the maximum number of concurrent batch requests to the LLM.
    /// This controls parallelism to avoid overwhelming the LLM service.
    /// Default is 3 concurrent requests.
    /// </summary>
    /// <remarks>
    /// Set lower values for rate-limited APIs or when cost control is important.
    /// Set higher values for faster processing when the LLM service can handle the load.
    /// </remarks>
    public int MaxConcurrentBatches { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum total rows to process per document.
    /// Documents exceeding this limit will be truncated with a warning.
    /// Default is 1000 rows.
    /// </summary>
    /// <remarks>
    /// This is a safety limit to prevent runaway processing costs.
    /// Set to 0 or negative to disable the limit (not recommended for production).
    /// </remarks>
    public int MaxRowsPerDocument { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to continue processing remaining batches when one batch fails.
    /// Default is true (continue processing).
    /// </summary>
    /// <remarks>
    /// When true, failed batches are marked with error messages in the output.
    /// When false, processing stops on the first batch failure.
    /// </remarks>
    public bool ContinueOnBatchFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout in seconds for each individual batch request.
    /// Default is 300 seconds (5 minutes) to accommodate large transcript analysis.
    /// </summary>
    public int BatchTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the delay in milliseconds between batch submissions.
    /// Helps avoid rate limiting when processing many batches.
    /// Default is 100ms. Set to 0 to disable.
    /// </summary>
    public int DelayBetweenBatchesMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the cache expiration time in minutes for batch processing results.
    /// Cached results allow follow-up questions without re-processing documents.
    /// Default is 30 minutes.
    /// </summary>
    /// <remarks>
    /// Set to 0 or negative to disable caching (not recommended for large files).
    /// Cache is invalidated when documents are added or removed from the interaction.
    /// </remarks>
    public int CacheExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to enable result caching for batch processing.
    /// When enabled, identical prompts with the same documents will use cached results.
    /// Default is true.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;
}
