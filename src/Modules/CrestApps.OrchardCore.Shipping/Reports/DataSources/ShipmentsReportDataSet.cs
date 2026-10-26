using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Shipping.Reports.DataSources;

/// <summary>
/// One row per shipment of an order: the order, the carrier and tracking number, the units sent, what its label cost,
/// and when it shipped, was delivered or came back. The store's note on the shipment and the links to its label and its
/// tracking page are never exposed.
/// </summary>
public sealed class ShipmentsReportDataSet : IndexedReportDataSet<Shipment, ShipmentIndex>, IShippingReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the shipment was created, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session shipments are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShipmentsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ShipmentsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ShippingSource.Shipments,
                stringLocalizer["Shipments"],
                stringLocalizer["One row per shipment of an order: its carrier, tracking number, units, label cost in the label's currency, status and dates."]),
            session,
            authorizationService,
            ShippingConstants.CollectionName,
            ShippingPermissions.FulfillOrders)
    {
        var S = stringLocalizer;
        var shipment = S["Shipment"].Value;
        var label = S["Label"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Shipment ID"], DataFieldType.Text, record => record.ItemId, shipment).Identifier();
        AddField(nameof(Shipment.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, shipment).Identifier(ShippingReportDataSource.Order());
        AddField(nameof(Shipment.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, shipment);
        AddField(nameof(Shipment.Number), S["Shipment number"], DataFieldType.Integer, record => record.Number, shipment).Description = S["The shipment's place among the order's shipments, from 1."].Value;
        AddField(nameof(Shipment.Status), S["Status"], DataFieldType.Text, record => record.Status, shipment);
        AddField(nameof(Shipment.Carrier), S["Carrier"], DataFieldType.Text, record => record.Carrier, shipment);
        AddField(nameof(Shipment.TrackingNumber), S["Tracking number"], DataFieldType.Text, record => record.TrackingNumber, shipment);
        AddField(nameof(Shipment.TrackingStatus), S["Carrier's latest status"], DataFieldType.Text, record => record.TrackingStatus, shipment);
        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, shipment);
        AddField(nameof(Shipment.ItemCount), S["Units"], DataFieldType.Integer, record => record.Lines?.Sum(line => line.Quantity) ?? 0, shipment);

        AddField("HasLabel", S["Label bought"], DataFieldType.Boolean, record => !string.IsNullOrEmpty(record.LabelUrl), label);
        AddField("Currency", S["Label currency"], DataFieldType.Text, record => record.LabelCurrency, label);
        AddField(nameof(Shipment.LabelCost), S["Label cost"], DataFieldType.Decimal, record => record.LabelCost, label).Description = S["What the carrier's label cost, in the label's currency."].Value;

        AddField(CreatedUtcField, S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Shipment.ShippedUtc), S["Shipped"], DataFieldType.DateTime, record => record.ShippedUtc, dates);
        AddField(nameof(Shipment.DeliveredUtc), S["Delivered"], DataFieldType.DateTime, record => record.DeliveredUtc, dates);
        AddField(nameof(Shipment.ReturnedUtc), S["Returned"], DataFieldType.DateTime, record => record.ReturnedUtc, dates);
        AddField(nameof(Shipment.ShippedEmailSentUtc), S["Buyer emailed"], DataFieldType.DateTime, record => record.ShippedEmailSentUtc, dates).Description = S["When the buyer was emailed that the shipment left."].Value;
        AddField(nameof(Shipment.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<ShipmentIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ShipmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ShipmentIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Shipment.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<ShipmentIndex> columns)
    {
        columns.Enum(nameof(Shipment.Status), index => index.Status);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the tracking number trimmed, so it isn't always the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(Shipment.TrackingNumber));

        return columns;
    }
}
