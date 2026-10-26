using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Companies.Reports.DataSources;

/// <summary>
/// Exposes the companies that buy from the store, with their buyers and price lists, as a data source of Orchard Core,
/// for reports and data pipelines. Each data set checks that the principal may read it.
/// </summary>
public sealed class CompanyReportDataSource : RecordDataSource, IDataSource
{
    private readonly ICompanyReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets of the companies.</param>
    public CompanyReportDataSource(IEnumerable<ICompanyReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.CompanySource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CompanyReportDataSource>("Companies");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CompanyReportDataSource>("The companies that buy from the store, with their buyers, and the price lists they buy on.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
