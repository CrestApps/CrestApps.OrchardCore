using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// A data set of the <see cref="AIChatReportDataSource"/>. Each feature registers its own data sets as this service,
/// so a data set whose feature is disabled is never listed.
/// </summary>
public interface IAIChatReportDataSet : IReportRecordDataSet
{
}
