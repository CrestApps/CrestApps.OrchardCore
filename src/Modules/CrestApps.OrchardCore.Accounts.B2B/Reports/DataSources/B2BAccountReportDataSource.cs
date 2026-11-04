using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Accounts.B2B.Reports.DataSources;

/// <summary>
/// Exposes the accounts that buy from the store, with their buyers and price lists, as a data source of Orchard Core,
/// for reports and data pipelines. Each data set checks that the principal may read it.
/// </summary>
public sealed class B2BAccountReportDataSource : RecordDataSource, IDataSource
{
    private readonly IB2BAccountReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="B2BAccountReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets of the accounts.</param>
    public B2BAccountReportDataSource(IEnumerable<IB2BAccountReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.B2BAccountSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<B2BAccountReportDataSource>("B2B accounts");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<B2BAccountReportDataSource>("The accounts that buy from the store, with their buyers, and the price lists they buy on.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
