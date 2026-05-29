using System.Data;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Context passed to <see cref="IContentImportRowFilter.ShouldSkipRowAsync"/> to evaluate a single row.
/// </summary>
public sealed class ContentImportRowFilterContext
{
    /// <summary>
    /// Gets or sets the data row being evaluated.
    /// </summary>
    public DataRow Row { get; set; }

    /// <summary>
    /// Gets or sets the data columns in the file.
    /// </summary>
    public DataColumnCollection Columns { get; set; }

    /// <summary>
    /// Gets or sets the content type definition.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the content transfer entry associated with this import.
    /// </summary>
    public ContentTransferEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the row index (1-based).
    /// </summary>
    public int RowIndex { get; set; }
}
