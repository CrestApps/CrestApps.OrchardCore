using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Shipping.Reports.DataSources;

/// <summary>
/// Exposes the store's shipments (with the order lines each one holds) and its shipping zones (with the methods each
/// zone offers) as a data source of Orchard Core, for reports and data pipelines. The data sets are registered as
/// <see cref="IShippingReportDataSet"/>s, and each data set checks that the principal may read it.
/// </summary>
public sealed class ShippingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IShippingReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippingReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled shipping features register.</param>
    public ShippingReportDataSource(IEnumerable<IShippingReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.ShippingSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ShippingReportDataSource>("Shipping");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<ShippingReportDataSource>("The store's shipments, with the order lines each one holds, and its shipping zones, with the methods each zone offers.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;

    /// <summary>
    /// Creates a reference to the identifier of the records of one of the shipping data sets.
    /// </summary>
    /// <param name="dataSet">The technical name of the data set.</param>
    /// <returns>The reference.</returns>
    internal static DataFieldReference Own(string dataSet)
    {
        return CommerceReportDataSets.Reference(CommerceReportDataSets.ShippingSource.Name, dataSet);
    }

    /// <summary>
    /// Creates a reference to the identifier of the orders.
    /// </summary>
    /// <returns>The reference.</returns>
    internal static DataFieldReference Order()
    {
        return CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders);
    }
}
