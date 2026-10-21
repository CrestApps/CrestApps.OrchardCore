using System.Data;
using System.Globalization;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Inventory.ContentTransfer;

/// <summary>
/// Imports and exports how a product's stock is kept (counted, backorders, low-stock threshold, ships from) and how
/// many are available to sell: one number for a product without variants, or each variant's, by SKU or by its options,
/// such as <c>TEE-S=4; M / Red=2</c>. Counts are set once the import has saved the product and its stock items exist,
/// keeping the units unpaid orders hold on hand besides them.
/// </summary>
public sealed class InventoryPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    private readonly IStockItemStore _stockItems;

    internal readonly IStringLocalizer S;

    public InventoryPartContentImportHandler(IStockItemStore stockItems, IStringLocalizer<InventoryPartContentImportHandler> stringLocalizer)
    {
        _stockItems = stockItems;
        S = stringLocalizer;
    }

    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        var prefix = (context?.ContentTypePartDefinition?.Name ?? nameof(InventoryPart)) + "_";

        return
        [
            new() { Name = prefix + nameof(InventoryPart.TrackInventory), Description = S["Whether stock is counted: true or false."] },
            new() { Name = prefix + nameof(InventoryPart.AllowBackorder), Description = S["Whether it keeps selling when none are left: true or false."] },
            new() { Name = prefix + nameof(InventoryPart.LowStockThreshold), Description = S["How few available count as low stock."] },
            new() { Name = prefix + "ShipsFrom", Description = S["When a product not out yet ships, such as 2026-12-01."] },
            new() { Name = prefix + "Stock", Description = S["How many are available to sell, for a product without variants."], AdditionalNames = ["Stock", "Quantity", "Inventory"] },
            new() { Name = prefix + "VariantStock", Description = S["How many of each variant are available, by SKU or options, such as TEE-S=4; M / Red=2."] },
        ];
    }

    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(InventoryPart);
        var columns = GetColumns(context).ToArray();
        var part = context.ContentItem.Get<InventoryPart>(name) ?? new InventoryPart();
        int? stock = null;
        Dictionary<string, int> variantStock = null;

        foreach (DataColumn column in context.Columns)
        {
            var value = context.Row[column]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (Is(column.ColumnName, columns[0]) && bool.TryParse(value, out var track))
            {
                part.TrackInventory = track;
            }
            else if (Is(column.ColumnName, columns[1]) && bool.TryParse(value, out var backorder))
            {
                part.AllowBackorder = backorder;
            }
            else if (Is(column.ColumnName, columns[2]) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold) && threshold >= 0)
            {
                part.LowStockThreshold = threshold;
            }
            else if (Is(column.ColumnName, columns[3]) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var shipsFrom))
            {
                // A day, kept as noon UTC on it, as the editor keeps it.
                part.ReleaseDateUtc = DateTime.SpecifyKind(shipsFrom.Date.AddHours(12), DateTimeKind.Utc);
            }
            else if (Is(column.ColumnName, columns[4]) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                stock = Math.Max(0, count);
            }
            else if (Is(column.ColumnName, columns[5]))
            {
                variantStock = ParseVariantStock(value);
            }
        }

        context.ContentItem.Apply(name, part);

        if (stock.HasValue || variantStock is { Count: > 0 })
        {
            var contentItemId = context.ContentItem.ContentItemId;

            // The stock items are made after the product is saved, so the counts are set then.
            ShellScope.AddDeferredTask(async scope =>
            {
                var services = scope.ServiceProvider;

                await services.GetRequiredService<InventorySyncService>().SyncAsync([contentItemId]);
                await services.GetRequiredService<InventoryCountApplier>().ApplyAsync(contentItemId, stock, variantStock);
            });
        }

        return Task.CompletedTask;
    }

    public async Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.ContentTypePartDefinition?.Name ?? nameof(InventoryPart);

        if (context.ContentItem.Get<InventoryPart>(name) is not { } part)
        {
            return;
        }

        var prefix = name + "_";

        context.Row[prefix + nameof(InventoryPart.TrackInventory)] = part.TrackInventory ? "true" : "false";
        context.Row[prefix + nameof(InventoryPart.AllowBackorder)] = part.AllowBackorder ? "true" : "false";
        context.Row[prefix + nameof(InventoryPart.LowStockThreshold)] = part.LowStockThreshold?.ToString(CultureInfo.InvariantCulture);
        context.Row[prefix + "ShipsFrom"] = part.ReleaseDateUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var items = (await _stockItems.ListByProductAsync(context.ContentItem.ContentItemId)).Where(item => !item.Archived).ToList();

        if (items.Count == 1 && string.IsNullOrEmpty(items[0].VariantId))
        {
            context.Row[prefix + "Stock"] = items[0].Available.ToString(CultureInfo.InvariantCulture);
        }
        else if (items.Count > 0)
        {
            context.Row[prefix + "VariantStock"] = string.Join("; ", items.Select(item => $"{(string.IsNullOrEmpty(item.Sku) ? item.Title : item.Sku)}={item.Available.ToString(CultureInfo.InvariantCulture)}"));
        }
    }

    /// <summary>
    /// Reads "TEE-S=4; M / Red=2".
    /// </summary>
    public static Dictionary<string, int> ParseVariantStock(string text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.LastIndexOf('=');

            if (separator > 0 && int.TryParse(pair[(separator + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                result[pair[..separator].Trim()] = Math.Max(0, count);
            }
        }

        return result;
    }
}
