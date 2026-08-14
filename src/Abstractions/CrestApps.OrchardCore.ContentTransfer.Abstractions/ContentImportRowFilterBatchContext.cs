using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Context passed to <see cref="IContentImportRowFilter.PrepareBatchAsync(ContentImportRowFilterBatchContext)"/>
/// so a filter can preload state for the next import batch.
/// </summary>
public sealed class ContentImportRowFilterBatchContext
{
    /// <summary>
    /// Gets or sets the content type definition for the import.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the content transfer entry associated with the import.
    /// </summary>
    public ContentTransferEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the rows that will be evaluated in the current batch.
    /// </summary>
    public IReadOnlyList<ContentImportRowFilterContext> Rows { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token for the current batch operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
