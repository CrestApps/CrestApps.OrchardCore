using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Transactions.Reports.DataSources;

/// <summary>
/// One data set of the transactions data source. Each feature registers the data sets of the records it reads, so the
/// data source lists only the data sets whose records are kept.
/// </summary>
public interface ITransactionReportDataSet : IRecordDataSet
{
}
