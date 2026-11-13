using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Reports;

/// <summary>
/// The Banking data source of Orchard Core, for reports and data pipelines: the bank accounts, without their numbers.
/// </summary>
public sealed class BankingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IBankingReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankingReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public BankingReportDataSource(IEnumerable<IBankingReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Banking";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<BankingReportDataSource>("Banking");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<BankingReportDataSource>("The bank accounts of the business and of the people and organizations it pays, without their numbers.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
