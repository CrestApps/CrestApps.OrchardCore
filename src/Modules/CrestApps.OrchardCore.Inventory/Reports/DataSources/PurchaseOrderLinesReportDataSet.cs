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
/// One row per stock item a purchase order asks for: the units ordered and received, and what they cost in the purchase
/// order's currency. Each row carries the purchase order's number, status, supplier, location and dates, so a report
/// can group the lines by them without joining the purchase orders.
/// </summary>
public sealed class PurchaseOrderLinesReportDataSet : IndexedReportDataSet<PurchaseOrder, PurchaseOrderIndex, ReportChildRow<PurchaseOrder, PurchaseOrderLine>>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the purchase order a line belongs to.
    /// </summary>
    public const string PurchaseOrderIdField = "PurchaseOrderId";

    /// <summary>
    /// The name of the field that holds when the purchase order was started, the data set's main date.
    /// </summary>
    public const string PurchaseOrderCreatedUtcField = "PurchaseOrderCreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseOrderLinesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session purchase orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PurchaseOrderLinesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PurchaseOrderLinesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.PurchaseOrderLines,
                stringLocalizer["Purchase order lines"],
                stringLocalizer["One row per stock item a purchase order asks for: the units ordered and received, and their cost in the purchase order's currency."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var order = S["Purchase order"].Value;
        var line = S["Line"].Value;
        var inCurrency = S["In the purchase order's currency."].Value;

        AddField(PurchaseOrderIdField, S["Purchase order ID"], DataFieldType.Text, row => row.Parent.ItemId, order)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.PurchaseOrders));
        AddField("PurchaseOrderNumber", S["Purchase order number"], DataFieldType.Text, row => row.Parent.Number, order);
        AddField("PurchaseOrderStatus", S["Purchase order status"], DataFieldType.Text, row => row.Parent.Status, order);
        AddField(nameof(PurchaseOrder.Supplier), S["Supplier"], DataFieldType.Text, row => row.Parent.Supplier, order);
        AddField(nameof(PurchaseOrder.LocationId), S["Location ID"], DataFieldType.Text, row => row.Parent.LocationId, order)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(PurchaseOrder.Currency), S["Currency"], DataFieldType.Text, row => row.Parent.Currency, order);
        AddField(PurchaseOrderCreatedUtcField, S["Purchase order started"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, order);
        AddField("PurchaseOrderReceivedUtc", S["Purchase order received"], DataFieldType.DateTime, row => row.Parent.ReceivedUtc, order);

        AddField(nameof(PurchaseOrderLine.LineId), S["Line ID"], DataFieldType.Text, row => row.Child.LineId, line).Identifier();
        AddField("Position", S["Line number"], DataFieldType.Integer, row => row.Position, line);
        AddField(nameof(PurchaseOrderLine.StockItemId), S["Stock item ID"], DataFieldType.Text, row => row.Child.StockItemId, line)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.StockItems));
        AddField(nameof(PurchaseOrderLine.Title), S["Item"], DataFieldType.Text, row => row.Child.Title, line);
        AddField(nameof(PurchaseOrderLine.Sku), S["SKU"], DataFieldType.Text, row => row.Child.Sku, line);
        AddField(nameof(PurchaseOrderLine.Ordered), S["Units ordered"], DataFieldType.Integer, row => row.Child.Ordered, line);
        AddField(nameof(PurchaseOrderLine.Received), S["Units received"], DataFieldType.Integer, row => row.Child.Received, line);
        AddField(nameof(PurchaseOrderLine.Outstanding), S["Units to come"], DataFieldType.Integer, row => row.Child.Outstanding, line);
        AddField(nameof(PurchaseOrderLine.UnitCost), S["Unit cost"], DataFieldType.Decimal, row => row.Child.UnitCost, line).Description = inCurrency;
        AddField("LineTotal", S["Line cost"], DataFieldType.Decimal, row => row.Child.UnitCost * row.Child.Ordered, line).Description = S["The units ordered at the unit cost, in the purchase order's currency."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<PurchaseOrderIndex, DateTime>> Column)? DateColumn
        => (PurchaseOrderCreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PurchaseOrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PurchaseOrderIndex, string>>>(StringComparer.Ordinal)
        {
            [PurchaseOrderIdField] = index => index.ItemId,
            [nameof(PurchaseOrder.LocationId)] = index => index.LocationId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PurchaseOrderIndex> columns)
    {
        columns
            .Enum("PurchaseOrderStatus", index => index.Status)
            .Text("PurchaseOrderNumber", nameof(PurchaseOrderIndex.Number));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<PurchaseOrder, PurchaseOrderLine>> GetRows(PurchaseOrder document)
    {
        return ReportChildRow<PurchaseOrder, PurchaseOrderLine>.For(document, document.Lines);
    }
}
