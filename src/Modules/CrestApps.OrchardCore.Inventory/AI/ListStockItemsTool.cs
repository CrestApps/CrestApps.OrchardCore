using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Lists the stock items of Commerce › Inventory (<see cref="IStockItemStore.PageAsync"/>, as the page does), searched by
/// the start of a SKU or title and filtered by all, low, out of stock or backordered. It needs
/// <see cref="InventoryPermissions.ViewInventory"/>.
/// </summary>
internal sealed class ListStockItemsTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listStockItems";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "The start of a SKU or of a product's title."
        },
        "filter": {
          "type": "string",
          "enum": ["all", "low", "out", "backordered"],
          "description": "low for items at or below their low-stock threshold (out of stock included), out for none available, backordered for items sold below zero. all when left out."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Stock items per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the products and variants the store counts, with SKU, units on hand, held and available, the low-stock threshold and whether backorders are allowed. Filters by text (SKU or title) and by all, low, out of stock or backordered, with paging; use it to answer stock questions and to get a stock item id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ViewInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var filterText = ReadString(arguments, "filter")?.ToLowerInvariant() ?? "all";
        var filter = filterText switch
        {
            "all" => StockItemFilter.All,
            "low" => StockItemFilter.Low,
            "out" => StockItemFilter.Out,
            "backordered" => StockItemFilter.Backordered,
            _ => (StockItemFilter?)null,
        };

        if (filter is null)
        {
            return "Nothing was listed: filter must be all, low, out or backordered.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (total, items) = await services.GetRequiredService<IStockItemStore>().PageAsync(new StockItemQuery
        {
            Search = ReadString(arguments, "query"),
            Filter = filter.Value,
        }, (page - 1) * pageSize, pageSize, cancellationToken);
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(new
        {
            items = items.Select(item => Describe(item, locations)).ToArray(),
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
        });
    }
}
