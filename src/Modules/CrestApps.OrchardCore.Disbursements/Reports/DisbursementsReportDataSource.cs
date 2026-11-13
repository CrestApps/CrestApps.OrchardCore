using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Disbursements.Reports;

/// <summary>
/// A data set of the Disbursements data source.
/// </summary>
public interface IDisbursementsReportDataSet : IRecordDataSet
{
}

/// <summary>
/// The Disbursements data source: the money the business sent out, for reports and data pipelines.
/// </summary>
public sealed class DisbursementsReportDataSource : RecordDataSource, IDataSource
{
    private readonly IDisbursementsReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementsReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public DisbursementsReportDataSource(IEnumerable<IDisbursementsReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => "Disbursements";

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DisbursementsReportDataSource>("Disbursements");

    /// <summary>
    /// Gets the data source's description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DisbursementsReportDataSource>("The money the business sent out: every disbursement, its method, status and dates.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
