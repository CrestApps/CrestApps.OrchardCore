using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// A data set of the Learning data source. Each Learning feature registers its own, so data sets of disabled features
/// are never listed.
/// </summary>
public interface ILearningReportDataSet : IRecordDataSet
{
}
