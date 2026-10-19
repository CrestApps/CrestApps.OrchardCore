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

    /// <summary>
    /// Gets or sets the reason the row should be skipped.
    /// </summary>
    public string SkipReason { get; set; }

    /// <summary>
    /// Gets or sets a short name for the kind of reason the row was skipped, the same for every row skipped for that
    /// reason, such as "On a national do-not-call registry". The import counts the rows skipped for each kind and
    /// keeps the kind of every skipped row, while <see cref="SkipReason"/> is kept for a limited number of rows only.
    /// </summary>
    public string SkipCategory { get; set; }
}
