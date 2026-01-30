namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Represents the result of processing a single batch of tabular rows.
/// </summary>
public sealed class TabularBatchResult
{
    /// <summary>
    /// Gets or sets the zero-based index of the batch that was processed.
    /// </summary>
    public int BatchIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the batch was processed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the one-based row index of the first data row in this batch result.
    /// </summary>
    public int RowStartIndex { get; set; }

    /// <summary>
    /// Gets or sets the one-based row index of the last data row in this batch result.
    /// </summary>
    public int RowEndIndex { get; set; }

    /// <summary>
    /// Gets or sets the number of data rows that were processed in this batch.
    /// </summary>
    public int ProcessedRowCount { get; set; }

    /// <summary>
    /// Gets or sets the LLM-generated output content for this batch.
    /// Contains one result per input row.
    /// </summary>
    public string OutputContent { get; set; }

    /// <summary>
    /// Gets or sets the error message if the batch processing failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful batch result.
    /// </summary>
    public static TabularBatchResult CreateSuccess(int batchIndex, int rowStart, int rowEnd, int rowCount, string output)
    {
        return new TabularBatchResult
        {
            BatchIndex = batchIndex,
            Success = true,
            RowStartIndex = rowStart,
            RowEndIndex = rowEnd,
            ProcessedRowCount = rowCount,
            OutputContent = output,
        };
    }

    /// <summary>
    /// Creates a failed batch result.
    /// </summary>
    public static TabularBatchResult CreateFailure(int batchIndex, int rowStart, int rowEnd, int rowCount, string error)
    {
        return new TabularBatchResult
        {
            BatchIndex = batchIndex,
            Success = false,
            RowStartIndex = rowStart,
            RowEndIndex = rowEnd,
            ProcessedRowCount = rowCount,
            ErrorMessage = error,
        };
    }
}
