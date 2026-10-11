namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Resolves the report data sources registered by the enabled features.
/// </summary>
public interface IReportDataSourceManager
{
    /// <summary>
    /// Lists every registered data source, ordered by display name.
    /// </summary>
    /// <returns>The data sources.</returns>
    IReadOnlyList<IReportDataSource> GetDataSources();

    /// <summary>
    /// Finds a data source by its technical name.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <returns>The data source, or <see langword="null"/> when none is registered under the name.</returns>
    IReportDataSource FindDataSource(string name);
}
