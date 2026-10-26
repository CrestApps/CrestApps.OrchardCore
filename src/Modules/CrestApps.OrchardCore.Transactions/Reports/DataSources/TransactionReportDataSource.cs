using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Transactions.Reports.DataSources;

/// <summary>
/// Exposes the transaction ledger, and the payments and refunds the checkout records, as a data source of Orchard Core,
/// for reports and data pipelines. The data sets are registered as <see cref="ITransactionReportDataSet"/>s: the
/// payments and refunds only while Checkout, which keeps them, is enabled. Each data set checks that the principal may
/// read it.
/// </summary>
public sealed class TransactionReportDataSource : RecordDataSource, IDataSource
{
    private readonly ITransactionReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled features register.</param>
    public TransactionReportDataSource(IEnumerable<ITransactionReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.TransactionSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TransactionReportDataSource>("Transactions");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<TransactionReportDataSource>("The transaction ledger, with the outstanding balances, and the payments and refunds of the checkout.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
