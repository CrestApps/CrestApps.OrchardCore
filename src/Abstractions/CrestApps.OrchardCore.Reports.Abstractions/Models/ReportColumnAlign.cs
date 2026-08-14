namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Identifies how a report table column is aligned.
/// </summary>
public enum ReportColumnAlign
{
    /// <summary>
    /// The column is aligned to the start (left in left-to-right cultures).
    /// </summary>
    Start,

    /// <summary>
    /// The column is centered.
    /// </summary>
    Center,

    /// <summary>
    /// The column is aligned to the end (right in left-to-right cultures), typical for numbers.
    /// </summary>
    End,
}
