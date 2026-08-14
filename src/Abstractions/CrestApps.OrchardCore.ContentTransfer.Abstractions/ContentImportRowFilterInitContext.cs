using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Context passed to <see cref="IContentImportRowFilter.InitializeAsync"/> before import processing begins.
/// </summary>
public sealed class ContentImportRowFilterInitContext
{
    /// <summary>
    /// Gets or sets the content type definition.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the content transfer entry associated with this import.
    /// </summary>
    public ContentTransferEntry Entry { get; set; }
}
