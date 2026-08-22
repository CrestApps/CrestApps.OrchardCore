using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// The context passed to <see cref="IContentExportBatchHandler"/> once per export page, before the content
/// items are mapped to rows. It lets a handler pre-load any external data it needs for the whole page in a
/// single pass, instead of querying once per row.
/// </summary>
public sealed class ContentExportBatchContext
{
    /// <summary>
    /// Gets or sets the content items that make up the current export page.
    /// </summary>
    public IReadOnlyList<ContentItem> ContentItems { get; set; }

    /// <summary>
    /// Gets or sets the definition of the content type being exported.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the export entry, which carries the options contributed to the export form.
    /// </summary>
    public ContentTransferEntry Entry { get; set; }
}
