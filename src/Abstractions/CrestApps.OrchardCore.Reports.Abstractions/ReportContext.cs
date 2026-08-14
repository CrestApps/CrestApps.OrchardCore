using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Provides the context passed to a report when it runs, exposing the full filter (including any values
/// contributed by filter display drivers, such as the built-in date range).
/// </summary>
public sealed class ReportContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportContext"/> class.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    public ReportContext(ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        Filter = filter;
    }

    /// <summary>
    /// Gets the report filter, including the extensible property bag of filter values. Read the date
    /// range with <see cref="ReportFilterExtensions.GetDateRange(ReportFilter)"/> and other filter
    /// values with the typed <see cref="ReportFilterExtensions"/> helpers.
    /// </summary>
    public ReportFilter Filter { get; }
}
