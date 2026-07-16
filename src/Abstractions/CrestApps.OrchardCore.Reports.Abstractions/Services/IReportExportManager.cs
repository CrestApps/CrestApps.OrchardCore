using CrestApps.OrchardCore.Reports;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Resolves the report export formats registered across the application.
/// </summary>
public interface IReportExportManager
{
    /// <summary>
    /// Lists every registered export format.
    /// </summary>
    /// <returns>The registered export formats.</returns>
    IReadOnlyList<IReportExportFormat> ListFormats();

    /// <summary>
    /// Finds an export format by its technical name.
    /// </summary>
    /// <param name="name">The format technical name.</param>
    /// <returns>The matching format, or <see langword="null"/> when none is registered.</returns>
    IReportExportFormat FindFormat(string name);
}
