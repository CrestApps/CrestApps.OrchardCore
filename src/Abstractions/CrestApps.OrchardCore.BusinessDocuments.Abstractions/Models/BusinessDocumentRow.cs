namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A row of the line table of a business document.
/// </summary>
public sealed class BusinessDocumentRow
{
    /// <summary>
    /// Gets or sets what the row is: an item, a section heading, a subtotal or a note.
    /// </summary>
    public BusinessDocumentRowKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the values of the row, keyed by <see cref="BusinessDocumentColumn.Name"/>. A section heading or a
    /// note prints the value of the first column across the whole row.
    /// </summary>
    public IDictionary<string, string> Cells { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets smaller text printed under the value of the first column, such as a description or a service
    /// period.
    /// </summary>
    public string Detail { get; set; }
}
