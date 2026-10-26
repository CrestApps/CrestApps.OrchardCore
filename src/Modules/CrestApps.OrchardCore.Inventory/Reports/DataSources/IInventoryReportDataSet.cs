using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Inventory.Reports.DataSources;

/// <summary>
/// One data set of the inventory data source. Each feature registers the data sets of the records it stores, so the
/// data source lists only the data sets of the features that are enabled.
/// </summary>
public interface IInventoryReportDataSet : IRecordDataSet
{
}
