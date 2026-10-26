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
/// One row per product or variant stocked at a location: its units on hand, held for orders and available there. An
/// item counted before there were locations has its units at the default location, and so has any part of its totals
/// no location accounts for, as on the item's admin page.
/// </summary>
public sealed class StockLevelsReportDataSet : IndexedReportDataSet<StockItem, StockItemIndex, ReportChildRow<StockItem, StockLocationLevel>>, IInventoryReportDataSet
{
    /// <summary>
    /// The name of the field that holds the stock item a level belongs to.
    /// </summary>
    public const string StockItemIdField = "StockItemId";

    /// <summary>
    /// Initializes a new instance of the <see cref="StockLevelsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session stock items are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StockLevelsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<StockLevelsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.InventorySource.StockLevels,
                stringLocalizer["Stock levels"],
                stringLocalizer["One row per product or variant stocked at a location: its units on hand, held for orders and available there."]),
            session,
            authorizationService,
            InventoryConstants.CollectionName,
            InventoryPermissions.ViewInventory)
    {
        var S = stringLocalizer;
        var item = S["Item"].Value;
        var stock = S["Stock"].Value;

        AddField(StockItemIdField, S["Stock item ID"], DataFieldType.Text, row => row.Parent.ItemId, item)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.StockItems));
        AddField(nameof(StockLocationLevel.LocationId), S["Location ID"], DataFieldType.Text, row => InventoryReportDataSource.LocationOf(row.Child.LocationId), item)
            .Identifier(InventoryReportDataSource.Own(CommerceReportDataSets.InventorySource.Locations));
        AddField(StockItemsReportDataSet.ProductIdField, S["Product ID"], DataFieldType.Text, row => row.Parent.ContentItemId, item)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(StockItem.VariantId), S["Variant ID"], DataFieldType.Text, row => row.Parent.VariantId, item)
            .Identifier(CommerceReportDataSets.VariantReference());
        AddField(nameof(StockItem.Sku), S["SKU"], DataFieldType.Text, row => row.Parent.Sku, item);
        AddField(nameof(StockItem.Title), S["Item"], DataFieldType.Text, row => row.Parent.Title, item);
        AddField(nameof(StockItem.Archived), S["Archived"], DataFieldType.Boolean, row => row.Parent.Archived, item);

        AddField(nameof(StockLocationLevel.OnHand), S["On hand"], DataFieldType.Integer, row => row.Child.OnHand, stock);
        AddField(nameof(StockLocationLevel.Reserved), S["Held for orders"], DataFieldType.Integer, row => row.Child.Reserved, stock);
        AddField(nameof(StockLocationLevel.Available), S["Available"], DataFieldType.Integer, row => row.Child.Available, stock).Description = S["The units on hand at the location less those held there for orders."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<StockItemIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<StockItemIndex, string>>>(StringComparer.Ordinal)
        {
            [StockItemIdField] = index => index.ItemId,
            [StockItemsReportDataSet.ProductIdField] = index => index.ContentItemId,
            [nameof(StockItem.VariantId)] = index => index.VariantId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<StockItem, StockLocationLevel>> GetRows(StockItem document)
    {
        return ReportChildRow<StockItem, StockLocationLevel>.For(document, LevelsOf(document));
    }

    // The units at each location, made to add up to the item's totals the way StockItem.NormalizeLevels does, without
    // changing the item read.
    private static List<StockLocationLevel> LevelsOf(StockItem item)
    {
        var levels = (item.Levels ?? [])
            .Where(level => level is not null)
            .Select(level => new StockLocationLevel
            {
                LocationId = InventoryReportDataSource.LocationOf(level.LocationId),
                OnHand = level.OnHand,
                Reserved = level.Reserved,
            })
            .ToList();

        var onHand = item.OnHand - levels.Sum(level => level.OnHand);
        var reserved = item.Reserved - levels.Sum(level => level.Reserved);

        if (onHand == 0 && reserved == 0)
        {
            return levels;
        }

        var fallback = levels.FirstOrDefault(level => level.LocationId == InventoryConstants.DefaultLocationId);

        if (fallback is null)
        {
            fallback = new StockLocationLevel { LocationId = InventoryConstants.DefaultLocationId };
            levels.Insert(0, fallback);
        }

        fallback.OnHand += onHand;
        fallback.Reserved += reserved;

        return levels;
    }
}
