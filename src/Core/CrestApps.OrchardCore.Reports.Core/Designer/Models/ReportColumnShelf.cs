namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies the shelf a column is placed on, which decides how the report's table lays it out.
/// </summary>
public enum ReportColumnShelf
{
    /// <summary>
    /// The column is on the Columns shelf. While no column is on the Rows shelf, the table shows each column side by
    /// side; once one is, the dimensions of the Columns shelf spread their values across the top of the table.
    /// </summary>
    Columns,

    /// <summary>
    /// The column is on the Rows shelf: a dimension whose values head the rows of the table, down its side.
    /// </summary>
    Rows,
}
