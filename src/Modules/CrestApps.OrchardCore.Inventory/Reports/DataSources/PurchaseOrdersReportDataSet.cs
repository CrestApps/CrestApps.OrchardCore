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
/// One row per order of stock from a supplier: the supplier, the location it is received at, the units ordered and
/// received, what it costs in its own currency, and where it stands. Its note is never exposed.
/// </summary>
public sealed class PurchaseOrdersReportDataSet : IndexedReportDataSet<PurchaseOrder, PurchaseOrderIndex>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the purchase order was started, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseOrdersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session purchase orders are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PurchaseOrdersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<PurchaseOrdersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.PurchaseOrders,
                stringLocalizer["Purchase orders"],
                stringLocalizer["One row per order of stock from a supplier: the supplier, the location, the units ordered and received, the cost in its own currency, the status and the dates."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var order = S["Purchase order"].Value;
        var units = S["Units and cost"].Value;
        var dates = S["Dates"].Value;
        var inCurrency = S["In the purchase order's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Purchase order ID"], DataFieldType.Text, record => record.ItemId, order).Identifier();
        AddField(nameof(PurchaseOrder.Number), S["Purchase order number"], DataFieldType.Text, record => record.Number, order);
        AddField(nameof(PurchaseOrder.Status), S["Status"], DataFieldType.Text, record => record.Status, order);
        AddField(nameof(PurchaseOrder.Supplier), S["Supplier"], DataFieldType.Text, record => record.Supplier, order);
        AddField(nameof(PurchaseOrder.SupplierEmail), S["Supplier's email"], DataFieldType.Text, record => record.SupplierEmail, order);
        AddField(nameof(PurchaseOrder.LocationId), S["Location ID"], DataFieldType.Text, record => record.LocationId, order)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(PurchaseOrder.OwnerId), S["Started by user ID"], DataFieldType.Text, record => record.OwnerId, order).Identifier(ReportDataReferences.User());
        AddField(nameof(PurchaseOrder.Author), S["Started by"], DataFieldType.Text, record => record.Author, order);

        AddField("LineCount", S["Lines"], DataFieldType.Integer, record => record.Lines?.Count ?? 0, units);
        AddField(nameof(PurchaseOrder.Ordered), S["Units ordered"], DataFieldType.Integer, record => record.Lines?.Sum(line => line.Ordered) ?? 0, units);
        AddField(nameof(PurchaseOrder.Received), S["Units received"], DataFieldType.Integer, record => record.Lines?.Sum(line => line.Received) ?? 0, units);
        AddField("Outstanding", S["Units to come"], DataFieldType.Integer, record => record.Lines?.Sum(line => line.Outstanding) ?? 0, units);
        AddField(nameof(PurchaseOrder.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, units);
        AddField(nameof(PurchaseOrder.Total), S["Total cost"], DataFieldType.Decimal, record => record.Lines?.Sum(line => line.UnitCost * line.Ordered) ?? 0m, units).Description = S["The units ordered at their unit costs, in the purchase order's currency."].Value;
        AddField("ReceivedCost", S["Cost received"], DataFieldType.Decimal, record => record.Lines?.Sum(line => line.UnitCost * line.Received) ?? 0m, units).Description = inCurrency;

        AddField(CreatedUtcField, S["Started"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(PurchaseOrder.OrderedUtc), S["Ordered"], DataFieldType.DateTime, record => record.OrderedUtc, dates);
        AddField(nameof(PurchaseOrder.ExpectedUtc), S["Expected"], DataFieldType.DateTime, record => record.ExpectedUtc, dates);
        AddField(nameof(PurchaseOrder.ReceivedUtc), S["Received"], DataFieldType.DateTime, record => record.ReceivedUtc, dates);
        AddField(nameof(PurchaseOrder.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<PurchaseOrderIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<PurchaseOrderIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<PurchaseOrderIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(PurchaseOrder.LocationId)] = index => index.LocationId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<PurchaseOrderIndex> columns)
    {
        columns
            .Enum(nameof(PurchaseOrder.Status), index => index.Status)
            .Text(nameof(PurchaseOrder.Number), nameof(PurchaseOrderIndex.Number));
    }
}
