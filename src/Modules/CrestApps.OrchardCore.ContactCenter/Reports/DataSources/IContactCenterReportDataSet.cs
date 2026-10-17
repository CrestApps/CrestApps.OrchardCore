using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources;

/// <summary>
/// One data set of the Contact Center report data source. Each feature registers the data sets of the records it
/// stores, so the data source lists only the data sets of the features that are enabled.
/// </summary>
public interface IContactCenterReportDataSet : IReportRecordDataSet
{
}
