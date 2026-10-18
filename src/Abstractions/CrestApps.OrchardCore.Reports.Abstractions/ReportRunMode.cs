namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Says what the document a report produces is for. A report that pages its rows on screen reads the mode to hand
/// an export every matching row instead of the page on screen.
/// </summary>
public enum ReportRunMode
{
    /// <summary>
    /// The document is rendered in the browser, with the filter editor.
    /// </summary>
    Display,

    /// <summary>
    /// The document is serialized by an export format, such as CSV.
    /// </summary>
    Export,
}
