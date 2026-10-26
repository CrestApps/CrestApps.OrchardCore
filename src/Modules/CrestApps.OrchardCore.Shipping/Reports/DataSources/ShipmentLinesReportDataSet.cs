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
/// One row per order line a shipment holds units of: the line, the product's title and SKU as they were, and how many
/// units were sent. Each row carries the shipment's order, number, status, carrier and dates, so a report can group the
/// lines by them without joining the shipments.
/// </summary>
public sealed class ShipmentLinesReportDataSet : IndexedReportDataSet<Shipment, ShipmentIndex, ReportChildRow<Shipment, ShipmentLine>>, IShippingReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the shipment a line belongs to.
    /// </summary>
    public const string ShipmentIdField = "ShipmentId";

    /// <summary>
    /// The name of the field that holds when the shipment was created, the data set's main date.
    /// </summary>
    public const string ShipmentCreatedUtcField = "ShipmentCreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session shipments are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShipmentLinesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ShipmentLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ShippingSource.ShipmentLines,
                stringLocalizer["Shipment lines"],
                stringLocalizer["One row per order line a shipment holds units of: the line, the product's title and SKU, and the units sent."]),
            session,
            authorizationService,
            ShippingConstants.CollectionName,
            ShippingPermissions.FulfillOrders)
    {
        var S = stringLocalizer;
        var shipment = S["Shipment"].Value;
        var line = S["Line"].Value;

        AddField(ShipmentIdField, S["Shipment ID"], DataFieldType.Text, row => row.Parent.ItemId, shipment)
            .Identifier(ShippingReportDataSource.Own(CommerceReportDataSets.ShippingSource.Shipments));
        AddField(nameof(Shipment.OrderId), S["Order ID"], DataFieldType.Text, row => row.Parent.OrderId, shipment).Identifier(ShippingReportDataSource.Order());
        AddField(nameof(Shipment.OrderNumber), S["Order number"], DataFieldType.Text, row => row.Parent.OrderNumber, shipment);
        AddField("ShipmentNumber", S["Shipment number"], DataFieldType.Integer, row => row.Parent.Number, shipment);
        AddField("ShipmentStatus", S["Shipment status"], DataFieldType.Text, row => row.Parent.Status, shipment);
        AddField(nameof(Shipment.Carrier), S["Carrier"], DataFieldType.Text, row => row.Parent.Carrier, shipment);
        AddField(ShipmentCreatedUtcField, S["Shipment created"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, shipment);
        AddField(nameof(Shipment.ShippedUtc), S["Shipped"], DataFieldType.DateTime, row => row.Parent.ShippedUtc, shipment);
        AddField(nameof(Shipment.DeliveredUtc), S["Delivered"], DataFieldType.DateTime, row => row.Parent.DeliveredUtc, shipment);

        AddField(nameof(ShipmentLine.LineId), S["Order line ID"], DataFieldType.Text, row => row.Child.LineId, line)
            .Identifier(new DataFieldReference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.OrderLines, nameof(ShipmentLine.LineId)));
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField(nameof(ShipmentLine.Title), S["Product"], DataFieldType.Text, row => row.Child.Title, line);
        AddField(nameof(ShipmentLine.Sku), S["SKU"], DataFieldType.Text, row => row.Child.Sku, line);
        AddField(nameof(ShipmentLine.Quantity), S["Units"], DataFieldType.Integer, row => row.Child.Quantity, line);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<ShipmentIndex, DateTime>> Column)? DateColumn
        => (ShipmentCreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ShipmentIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ShipmentIndex, string>>>(StringComparer.Ordinal)
        {
            [ShipmentIdField] = index => index.ItemId,
            [nameof(Shipment.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<ShipmentIndex> columns)
    {
        columns.Enum("ShipmentStatus", index => index.Status);
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<Shipment, ShipmentLine>> GetRows(Shipment document)
    {
        return ReportChildRow<Shipment, ShipmentLine>.For(document, document.Lines);
    }
}
