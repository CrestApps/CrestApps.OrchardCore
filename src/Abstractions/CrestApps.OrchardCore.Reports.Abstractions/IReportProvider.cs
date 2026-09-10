namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Contributes one or more reports to the Reports framework from a single registration. A module that
/// exposes a family of reports driven by data (for example, a catalog of report definitions) implements
/// this interface instead of registering each <see cref="IReport"/> individually, keeping the reports
/// extensible and their metadata out of imperative service-registration code.
/// </summary>
public interface IReportProvider
{
    /// <summary>
    /// Gets the reports contributed by this provider.
    /// </summary>
    /// <returns>The reports to surface under the admin Reports area.</returns>
    IEnumerable<IReport> GetReports();
}
