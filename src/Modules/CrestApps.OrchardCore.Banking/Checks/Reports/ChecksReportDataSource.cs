using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Reports;

/// <summary>
/// The Checks data source of Orchard Core, for reports and data pipelines, such as the bank's positive pay file.
/// </summary>
public sealed class ChecksReportDataSource : RecordDataSource, IDataSource
{
    private readonly IChecksReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecksReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public ChecksReportDataSource(IEnumerable<IChecksReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Checks";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ChecksReportDataSource>("Checks");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ChecksReportDataSource>("The check register: every check issued, printed, voided, stopped and cleared.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
