namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Identifies the semantic purpose of a report table row.
/// </summary>
public enum ReportRowKind
{
    /// <summary>
    /// A standard detail row.
    /// </summary>
    Detail,

    /// <summary>
    /// A subtotal for a group of detail rows.
    /// </summary>
    Subtotal,

    /// <summary>
    /// A grand total for the entire table.
    /// </summary>
    GrandTotal,
}
