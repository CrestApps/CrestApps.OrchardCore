namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Provides access to the reports registered across the application.
/// </summary>
public interface IReportManager
{
    /// <summary>
    /// Lists the descriptors of every registered report, without creating the reports.
    /// </summary>
    /// <returns>The registered reports, in the order they were registered.</returns>
    IReadOnlyList<ReportDescriptor> GetReports();

    /// <summary>
    /// Finds the descriptor of a registered report by its technical name.
    /// </summary>
    /// <param name="name">The report technical name.</param>
    /// <returns>The descriptor, or <see langword="null"/> when no report has that name.</returns>
    ReportDescriptor GetDescriptor(string name);

    /// <summary>
    /// Creates a registered report, to run it.
    /// </summary>
    /// <param name="name">The report technical name.</param>
    /// <returns>The report, or <see langword="null"/> when no report has that name.</returns>
    IReportRunner GetReport(string name);
}
