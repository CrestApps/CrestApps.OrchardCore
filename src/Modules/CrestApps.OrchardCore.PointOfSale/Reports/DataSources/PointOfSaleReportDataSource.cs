using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.PointOfSale.Reports.DataSources;

/// <summary>
/// Exposes the store's registers and the shifts worked at them as a data source of Orchard Core, for reports and data
/// pipelines. The data sets are registered as <see cref="IPointOfSaleReportDataSet"/>s, and each data set checks that
/// the principal may read it.
/// </summary>
public sealed class PointOfSaleReportDataSource : RecordDataSource, IDataSource
{
    private readonly IPointOfSaleReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="PointOfSaleReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled point of sale features register.</param>
    public PointOfSaleReportDataSource(IEnumerable<IPointOfSaleReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.PointOfSaleSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PointOfSaleReportDataSource>("Point of Sale");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<PointOfSaleReportDataSource>("The store's registers and the shifts worked at them: their staff, the cash in the drawer, and what each shift sold.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
