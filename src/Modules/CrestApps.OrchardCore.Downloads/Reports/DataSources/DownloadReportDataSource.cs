using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Downloads.Reports.DataSources;

/// <summary>
/// Exposes the files buyers may download as a data source of Orchard Core, for reports and data pipelines. The data
/// sets are registered as <see cref="IDownloadReportDataSet"/>s, and each data set checks that the principal may read
/// it.
/// </summary>
public sealed class DownloadReportDataSource : RecordDataSource, IDataSource
{
    private readonly IDownloadReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled downloads features register.</param>
    public DownloadReportDataSource(IEnumerable<IDownloadReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.DownloadSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DownloadReportDataSource>("Downloads");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DownloadReportDataSource>("The files buyers may download from their orders: the downloads made and left, and whether each link still works.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
