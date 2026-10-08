namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// An optional export hook that runs once per export page, before the page's content items are mapped to
/// rows. Implement it alongside <see cref="IContentImportHandler"/> to pre-load per-page data in a single
/// query and cache it, so the row-level <see cref="IContentImportHandler.ExportAsync"/> does not query one
/// record at a time.
/// </summary>
public interface IContentExportBatchHandler
{
    /// <summary>
    /// Pre-loads any data the handler needs for the whole export page.
    /// </summary>
    /// <param name="context">The batch context containing the page's content items and the export entry.</param>
    Task PrepareExportBatchAsync(ContentExportBatchContext context);
}
