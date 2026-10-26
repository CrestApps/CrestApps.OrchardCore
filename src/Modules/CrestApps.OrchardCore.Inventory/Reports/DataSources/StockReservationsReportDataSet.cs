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
/// One row per hold of units of a stock item for an order: how many, where, until when, and how it ended. The index
/// holds no date the hold was made, so the data set has no main date and reads the newest holds first.
/// </summary>
public sealed class StockReservationsReportDataSet : IndexedReportDataSet<StockReservation, StockReservationIndex>, IInventoryReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StockReservationsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session stock reservations are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StockReservationsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StockReservationsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.StockReservations,
                stringLocalizer["Stock reservations"],
                stringLocalizer["One row per hold of units of a stock item for an order: the units, the location, when the hold expires and how it ended."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var reservation = S["Reservation"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Reservation ID"], DataFieldType.Text, record => record.ItemId, reservation).Identifier();
        AddField(nameof(StockReservation.StockItemId), S["Stock item ID"], DataFieldType.Text, record => record.StockItemId, reservation)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.StockItems));
        AddField(nameof(StockReservation.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, reservation)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(StockReservation.LocationId), S["Location ID"], DataFieldType.Text, record => InventoryReportDataSource.LocationOf(record.LocationId), reservation)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(StockReservation.Status), S["Status"], DataFieldType.Text, record => record.Status, reservation);
        AddField(nameof(StockReservation.Quantity), S["Units"], DataFieldType.Integer, record => record.Quantity, reservation);
        AddField(nameof(StockReservation.ReturnedQuantity), S["Units returned"], DataFieldType.Integer, record => record.ReturnedQuantity, reservation).Description = S["How many of the units sold came back into stock."].Value;
        AddField(nameof(StockReservation.ReleaseReason), S["Release reason"], DataFieldType.Text, record => record.ReleaseReason, reservation);

        AddField(nameof(StockReservation.CreatedUtc), S["Held"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(StockReservation.ExpiresUtc), S["Hold expires"], DataFieldType.DateTime, record => record.ExpiresUtc, dates);
        AddField(nameof(StockReservation.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StockReservationIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StockReservationIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(StockReservation.StockItemId)] = index => index.StockItemId,
            [nameof(StockReservation.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<StockReservationIndex> columns)
    {
        columns.Enum(nameof(StockReservation.Status), index => index.Status);
    }
}
