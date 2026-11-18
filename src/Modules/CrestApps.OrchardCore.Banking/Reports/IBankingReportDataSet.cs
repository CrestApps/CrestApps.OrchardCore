using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Banking.Reports;

/// <summary>
/// A data set of the Banking data source. Modules that pay through bank accounts register theirs, so the data source
/// lists only those of the enabled features.
/// </summary>
public interface IBankingReportDataSet : IRecordDataSet
{
}
