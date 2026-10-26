using System.Linq.Expressions;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.Reports.DataSources;

/// <summary>
/// One row per change to the units on hand of a stock item, from the ledger that is never changed: what changed, by how
/// much, where, why, by whom, and the order or purchase order that caused it.
/// </summary>
public sealed class StockMovementsReportDataSet : IndexedReportDataSet<StockMovement, StockMovementIndex>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the change was made, the data set's main date.
    /// </summary>
    public const string UtcField = "Utc";

    /// <summary>
    /// Initializes a new instance of the <see cref="StockMovementsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session stock movements are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StockMovementsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StockMovementsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.StockMovements,
                stringLocalizer["Stock movements"],
                stringLocalizer["One row per change to the units on hand of a stock item: its kind, the change in units, the location, the reason, who made it and the order or purchase order that caused it."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var movement = S["Movement"].Value;
        var cause = S["Cause"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Movement ID"], DataFieldType.Text, record => record.ItemId, movement).Identifier();
        AddField(nameof(StockMovement.StockItemId), S["Stock item ID"], DataFieldType.Text, record => record.StockItemId, movement)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.StockItems));
        AddField(nameof(StockMovement.LocationId), S["Location ID"], DataFieldType.Text, record => InventoryReportDataSource.LocationOf(record.LocationId), movement)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(StockMovement.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, movement);
        AddField(nameof(StockMovement.Quantity), S["Change in units"], DataFieldType.Integer, record => record.Quantity, movement).Description = S["Positive when units came in, negative when they went out."].Value;
        AddField(nameof(StockMovement.OnHandAfter), S["On hand after"], DataFieldType.Integer, record => record.OnHandAfter, movement);
        AddField(UtcField, S["Date"], DataFieldType.DateTime, record => record.Utc, movement);

        AddField(nameof(StockMovement.Reason), S["Reason"], DataFieldType.Text, record => record.Reason, cause);
        AddField(nameof(StockMovement.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, cause)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(StockMovement.PurchaseOrderId), S["Purchase order ID"], DataFieldType.Text, record => record.PurchaseOrderId, cause)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.PurchaseOrders));
        AddField(nameof(StockMovement.ActorId), S["Changed by user ID"], DataFieldType.Text, record => record.ActorId, cause).Identifier(ReportDataReferences.User());
        AddField(nameof(StockMovement.ActorName), S["Changed by"], DataFieldType.Text, record => record.ActorName, cause);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<StockMovementIndex, DateTime>> Column)? DateColumn
        => (UtcField, index => index.Utc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StockMovementIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StockMovementIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(StockMovement.StockItemId)] = index => index.StockItemId,
            [nameof(StockMovement.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<StockMovementIndex> columns)
    {
        columns.Enum(nameof(StockMovement.Kind), index => index.Kind);
    }
}
