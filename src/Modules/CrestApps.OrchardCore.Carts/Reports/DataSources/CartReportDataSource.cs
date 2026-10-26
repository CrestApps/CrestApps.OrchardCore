using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Carts.Reports.DataSources;

/// <summary>
/// Exposes the shoppers' carts, with their lines, and their wishlists as a data source of Orchard Core, for reports and
/// data pipelines. Each enabled carts feature registers the data sets of the records it stores as
/// <see cref="ICartReportDataSet"/>s, so a data set whose feature is off is never listed, and each data set checks that
/// the principal may read it.
/// </summary>
public sealed class CartReportDataSource : RecordDataSource, IDataSource
{
    private readonly ICartReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CartReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled carts features register.</param>
    public CartReportDataSource(IEnumerable<ICartReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.CartSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CartReportDataSource>("Carts");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CartReportDataSource>("The shoppers' open carts, with their lines, and their wishlists. What an order buys leaves its cart, and empty and expired carts are deleted.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
