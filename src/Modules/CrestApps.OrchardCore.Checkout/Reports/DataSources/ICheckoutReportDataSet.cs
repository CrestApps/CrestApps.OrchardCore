using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// One data set of the checkout data source. Each feature registers the data sets of the records it stores, so the
/// data source lists only the data sets of the features that are enabled.
/// </summary>
public interface ICheckoutReportDataSet : IRecordDataSet
{
}
