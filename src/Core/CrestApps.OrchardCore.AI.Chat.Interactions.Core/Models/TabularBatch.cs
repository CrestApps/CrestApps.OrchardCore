namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Represents a batch of rows from a tabular document for processing.
/// Each batch contains a header row and a subset of data rows.
/// </summary>
public sealed class TabularBatch
{
    /// <summary>
    /// Gets or sets the zero-based index of this batch within the document.
    /// </summary>
    public int BatchIndex { get; set; }

    /// <summary>
    /// Gets or sets the source file name this batch originated from.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the header row content (first row of the tabular data).
    /// This is included with every batch to provide column context.
    /// </summary>
    public string HeaderRow { get; set; }

    /// <summary>
    /// Gets or sets the data rows for this batch (excluding header).
    /// </summary>
    public IList<string> DataRows { get; set; } = [];

    /// <summary>
    /// Gets or sets the one-based row index of the first data row in this batch
    /// relative to the original document (excluding header).
    /// </summary>
    public int RowStartIndex { get; set; }

    /// <summary>
    /// Gets or sets the one-based row index of the last data row in this batch
    /// relative to the original document (excluding header).
    /// </summary>
    public int RowEndIndex { get; set; }

    /// <summary>
    /// Gets the total number of data rows in this batch.
    /// </summary>
    public int RowCount => DataRows?.Count ?? 0;

    /// <summary>
    /// Gets the complete batch content with header and data rows combined.
    /// </summary>
    /// <returns>The combined tabular content as a string.</returns>
    public string GetContent()
    {
        if (string.IsNullOrEmpty(HeaderRow))
        {
            return string.Join('\n', DataRows ?? []);
        }

        var allRows = new List<string>(1 + (DataRows?.Count ?? 0)) { HeaderRow };
        
        if (DataRows is not null)
        {
            allRows.AddRange(DataRows);
        }

        return string.Join('\n', allRows);
    }
}
