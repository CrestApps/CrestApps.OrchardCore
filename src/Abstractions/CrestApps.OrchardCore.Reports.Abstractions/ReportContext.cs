using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Provides the context passed to a report when it runs, exposing the full filter (including any values
/// contributed by filter display drivers, such as the built-in date range) and what the document is for.
/// </summary>
public sealed class ReportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportContext"/> class for a document rendered in the browser.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    public ReportContext(ReportFilter filter)
        : this(filter, ReportRunMode.Display)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportContext"/> class.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    /// <param name="mode">What the document is for: the browser, or an export.</param>
    public ReportContext(ReportFilter filter, ReportRunMode mode)
    {
        ArgumentNullException.ThrowIfNull(filter);

        Filter = filter;
        Mode = mode;
    }

    /// <summary>
    /// Gets the report filter, including the extensible property bag of filter values. Read the date
    /// range with <see cref="ReportFilterExtensions.GetDateRange(ReportFilter)"/> and other filter
    /// values with the typed <see cref="ReportFilterExtensions"/> helpers.
    /// </summary>
    public ReportFilter Filter { get; }

    /// <summary>
    /// Gets what the document is for. A report that pages its rows on screen gives an export every matching row.
    /// </summary>
    public ReportRunMode Mode { get; }
}
