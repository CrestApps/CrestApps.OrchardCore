namespace CrestApps.OrchardCore.Reports.Contents.Models;

/// <summary>
/// Identifies how a <see cref="ContentElementReportField"/> turns a JSON property into a report value.
/// </summary>
public enum ContentReportValueMode
{
    /// <summary>
    /// The property holds a single value.
    /// </summary>
    Single,

    /// <summary>
    /// The property holds an array; the value is its first entry.
    /// </summary>
    First,

    /// <summary>
    /// The property holds an array; the value is its non-empty entries joined with commas (for example
    /// <c>a,b,c</c>), or <see langword="null"/> when it has none.
    /// </summary>
    Join,
}
