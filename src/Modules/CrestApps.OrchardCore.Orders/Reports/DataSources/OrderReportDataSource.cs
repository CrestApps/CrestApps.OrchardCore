using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// Exposes the store's orders (with their lines, refunds, disputes, discounts and taxes), returns and gift cards as a
/// data source of Orchard Core, for reports and data pipelines. Each enabled orders feature registers the data sets of
/// the records it stores as <see cref="IOrderReportDataSet"/>s, so a data set whose feature is off is never listed, and
/// each data set checks that the principal may read it.
/// </summary>
public sealed class OrderReportDataSource : RecordDataSource, IDataSource
{
    private readonly IOrderReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled orders features register.</param>
    public OrderReportDataSource(IEnumerable<IOrderReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.OrderSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderReportDataSource>("Orders");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderReportDataSource>("The store's orders, with their lines, refunds, disputes, discounts and taxes, and its returns and gift cards.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
