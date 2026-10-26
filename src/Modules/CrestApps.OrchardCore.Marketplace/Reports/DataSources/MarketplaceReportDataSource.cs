using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Marketplace.Reports.DataSources;

/// <summary>
/// Exposes the marketplace's vendors, their earnings and their payouts as a data source of Orchard Core, for reports and
/// data pipelines. The data sets are registered as <see cref="IMarketplaceReportDataSet"/>s, and each data set checks
/// that the principal may read it.
/// </summary>
public sealed class MarketplaceReportDataSource : RecordDataSource, IDataSource
{
    private readonly IMarketplaceReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketplaceReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled marketplace features register.</param>
    public MarketplaceReportDataSource(IEnumerable<IMarketplaceReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.MarketplaceSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MarketplaceReportDataSource>("Marketplace");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<MarketplaceReportDataSource>("The marketplace's vendors, what they earned on each sale after the store's commission, and what was paid out to them.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
