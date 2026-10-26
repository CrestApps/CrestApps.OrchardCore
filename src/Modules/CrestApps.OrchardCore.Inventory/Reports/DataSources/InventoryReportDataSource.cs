using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Inventory.Reports.DataSources;

/// <summary>
/// Exposes the store's stock (its items, their units at each location, movements and holds for orders), the places
/// stock is kept, purchase orders and back-in-stock alerts as a data source of Orchard Core, for reports and data
/// pipelines. Each enabled inventory feature registers the data sets of the records it stores as
/// <see cref="IInventoryReportDataSet"/>s, so a data set whose feature is off is never listed, and each data set checks
/// that the principal may read it.
/// </summary>
public sealed class InventoryReportDataSource : RecordDataSource, IDataSource
{
    private readonly IInventoryReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled inventory features register.</param>
    public InventoryReportDataSource(IEnumerable<IInventoryReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.InventorySource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<InventoryReportDataSource>("Inventory");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<InventoryReportDataSource>("The store's stock: items, their units at each location, movements, holds for orders and locations, with purchase orders and back-in-stock alerts.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;

    /// <summary>
    /// Creates a reference to the identifier of the records of one of the inventory data sets.
    /// </summary>
    /// <param name="dataSet">The technical name of the data set.</param>
    /// <returns>The reference.</returns>
    internal static DataFieldReference Own(string dataSet)
    {
        return CommerceReportDataSets.Reference(CommerceReportDataSets.InventorySource.Name, dataSet);
    }

    /// <summary>
    /// Gets the location a record names, the default location when it names none.
    /// </summary>
    /// <param name="locationId">The location the record names.</param>
    /// <returns>The identifier of the location.</returns>
    internal static string LocationOf(string locationId)
    {
        return string.IsNullOrEmpty(locationId) ? InventoryConstants.DefaultLocationId : locationId;
    }
}
