namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// One level of the result sort order.
/// </summary>
public sealed class ReportSortDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the column to sort by.
    /// </summary>
    public string ColumnId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column sorts from largest to smallest.
    /// </summary>
    public bool Descending { get; set; }
}
