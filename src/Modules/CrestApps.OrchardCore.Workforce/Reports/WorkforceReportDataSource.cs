using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// The Workforce report data source: the data sets of the Workforce features that are on, each limited to the teams
/// the reader manages.
/// </summary>
public sealed class WorkforceReportDataSource : RecordDataSource, IDataSource
{
    private readonly IWorkforceReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public WorkforceReportDataSource(IEnumerable<IWorkforceReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Workforce";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<WorkforceReportDataSource>("Workforce");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<WorkforceReportDataSource>("Punches, timesheet lines, attendance exceptions, time-off requests and predictability pay of the teams the reader manages.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
