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
/// One row per product or variant whose stock is counted: its units on hand, held for orders and available at every
/// location together, its low-stock threshold, and how it stands. Stock describes the moment the report runs, so the
/// data set has no main date; the stock levels data set gives the units at each location.
/// </summary>
public sealed class StockItemsReportDataSet : IndexedReportDataSet<StockItem, StockItemIndex>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds the product counted.
    /// </summary>
    public const string ProductIdField = "ProductId";

    /// <summary>
    /// Initializes a new instance of the <see cref="StockItemsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session stock items are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StockItemsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StockItemsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.StockItems,
                stringLocalizer["Stock items"],
                stringLocalizer["One row per product or variant whose stock is counted: its units on hand, held for orders and available at every location together, and how it stands."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var item = S["Item"].Value;
        var stock = S["Stock"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Stock item ID"], DataFieldType.Text, record => record.ItemId, item).Identifier();
        AddField(ProductIdField, S["Product ID"], DataFieldType.Text, record => record.ContentItemId, item)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(StockItem.VariantId), S["Variant ID"], DataFieldType.Text, record => record.VariantId, item)
            .Identifier(CommerceReportDataSets.VariantReference());
        AddField(nameof(StockItem.Sku), S["SKU"], DataFieldType.Text, record => record.Sku, item);
        AddField(nameof(StockItem.Title), S["Item"], DataFieldType.Text, record => record.Title, item);
        AddField(nameof(StockItem.Archived), S["Archived"], DataFieldType.Boolean, record => record.Archived, item).Description = S["The product or variant is gone or no longer counted. Its history is kept."].Value;

        AddField(nameof(StockItem.OnHand), S["On hand"], DataFieldType.Integer, record => record.OnHand, stock);
        AddField(nameof(StockItem.Reserved), S["Held for orders"], DataFieldType.Integer, record => record.Reserved, stock);
        AddField(nameof(StockItem.Available), S["Available"], DataFieldType.Integer, record => record.Available, stock).Description = S["The units on hand less those held for orders. Below zero when backorders were sold."].Value;
        AddField(nameof(StockItem.LowStockThreshold), S["Low-stock threshold"], DataFieldType.Integer, record => record.LowStockThreshold, stock).Description = S["How few available units count as low stock."].Value;
        AddField("StockStatus", S["Stock status"], DataFieldType.Text, record => StockLevels.Of(record.Available, record.LowStockThreshold), stock);
        AddField(nameof(StockItem.IsLow), S["Low"], DataFieldType.Boolean, record => record.IsLow, stock).Description = S["The available units are at or below the low-stock threshold, out of stock included."].Value;
        AddField("IsBackordered", S["Backordered"], DataFieldType.Boolean, record => record.Available < 0, stock);
        AddField(nameof(StockItem.AllowBackorder), S["Takes backorders"], DataFieldType.Boolean, record => record.AllowBackorder, stock);
        AddField(nameof(StockItem.ReleaseDateUtc), S["Ships from"], DataFieldType.DateTime, record => record.ReleaseDateUtc, stock);

        AddField(nameof(StockItem.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(StockItem.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StockItemIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StockItemIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [ProductIdField] = index => index.ContentItemId,
            [nameof(StockItem.VariantId)] = index => index.VariantId,
        };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the product in its ContentItemId column.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal)
        {
            [ProductIdField] = nameof(StockItemIndex.ContentItemId),
        };

        return columns;
    }
}
