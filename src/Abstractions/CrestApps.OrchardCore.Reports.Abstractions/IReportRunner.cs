using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Runs a report: builds its document for a filter. An <see cref="IReport"/> is one; so is each report of a family a
/// module describes with one <see cref="ReportDescriptor"/> per report, such as a catalog of reports that differ only
/// by what they measure.
/// </summary>
public interface IReportRunner
{
    /// <summary>
    /// Runs the report for the supplied filter and returns the resulting document.
    /// </summary>
    /// <param name="context">The report context, including the resolved period and filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The report document to render and export.</returns>
    Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);
}
