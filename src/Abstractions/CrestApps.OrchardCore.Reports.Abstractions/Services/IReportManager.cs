using CrestApps.OrchardCore.Reports;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Provides access to the reports registered across the application.
/// </summary>
public interface IReportManager
{
    /// <summary>
    /// Lists every registered report, ordered by category and display name.
    /// </summary>
    /// <returns>The registered reports.</returns>
    IReadOnlyList<IReport> ListReports();

    /// <summary>
    /// Finds a registered report by its technical name.
    /// </summary>
    /// <param name="name">The report technical name.</param>
    /// <returns>The matching report, or <see langword="null"/> when none is registered.</returns>
    IReport FindByName(string name);
}
