using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Reports;

/// <summary>
/// A data set of the Approvals data source.
/// </summary>
public interface IApprovalsReportDataSet : IRecordDataSet
{
}

/// <summary>
/// The Approvals data source: who asked for what, who decided and how long it took, for reports and data pipelines.
/// </summary>
public sealed class ApprovalsReportDataSource : RecordDataSource, IDataSource
{
    private readonly IApprovalsReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalsReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public ApprovalsReportDataSource(IEnumerable<IApprovalsReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Approvals";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ApprovalsReportDataSource>("Approvals");

    /// <summary>
    /// Gets the data source's description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ApprovalsReportDataSource>("Approval requests: what was asked, under which policy, who decided and when.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
