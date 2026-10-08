using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The default <see cref="IReportDataSourceManager"/> over the registered <see cref="IReportDataSource"/> services.
/// </summary>
public sealed class ReportDataSourceManager : IReportDataSourceManager
{
    private readonly IReadOnlyList<IReportDataSource> _dataSources;
    private readonly Dictionary<string, IReportDataSource> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDataSourceManager"/> class.
    /// </summary>
    /// <param name="dataSources">The registered data sources.</param>
    public ReportDataSourceManager(IEnumerable<IReportDataSource> dataSources)
    {
        foreach (var dataSource in dataSources)
        {
            if (string.IsNullOrEmpty(dataSource.Name))
            {
                continue;
            }

            if (!_byName.TryAdd(dataSource.Name, dataSource))
            {
                throw new InvalidOperationException($"A report data source named '{dataSource.Name}' is already registered.");
            }
        }

        _dataSources = _byName.Values
            .OrderBy(dataSource => dataSource.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IReportDataSource> GetDataSources()
    {
        return _dataSources;
    }

    /// <inheritdoc/>
    public IReportDataSource FindDataSource(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _byName.GetValueOrDefault(name);
    }
}
