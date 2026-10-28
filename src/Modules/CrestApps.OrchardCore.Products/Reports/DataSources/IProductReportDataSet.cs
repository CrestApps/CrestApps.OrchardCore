using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One data set of the products data source. Each feature registers the data sets of the records it keeps, so the data
/// source lists only the data sets of the features that are enabled.
/// </summary>
public interface IProductReportDataSet : IRecordDataSet
{
}
