using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Provides the context passed to a report when it runs, exposing the resolved reporting period and the
/// full filter (including any report-specific values contributed by filter display drivers).
/// </summary>
public sealed class ReportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportContext"/> class.
    /// </summary>
    /// <param name="filter">The report filter. Its from/to values are guaranteed to be set by the caller.</param>
    public ReportContext(ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        Filter = filter;
    }

    /// <summary>
    /// Gets the report filter, including the extensible property bag of report-specific filter values.
    /// </summary>
    public ReportFilter Filter { get; }

    /// <summary>
    /// Gets the inclusive lower UTC bound of the reporting period, as contributed by the built-in
    /// date-range filter. Returns the default value when the report does not contribute a date range.
    /// </summary>
    public DateTime FromUtc
    {
        get
        {
            return Filter.GetDateRange().FromUtc.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Gets the inclusive upper UTC bound of the reporting period, as contributed by the built-in
    /// date-range filter. Returns the default value when the report does not contribute a date range.
    /// </summary>
    public DateTime ToUtc
    {
        get
        {
            return Filter.GetDateRange().ToUtc.GetValueOrDefault();
        }
    }
}
