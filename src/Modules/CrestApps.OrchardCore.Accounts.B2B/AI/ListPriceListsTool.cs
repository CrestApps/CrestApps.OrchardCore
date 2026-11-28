using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Lists price lists by name, as Commerce › Price lists does (<see cref="IPriceListStore.PageAsync"/>), or returns one
/// price list with its prices by SKU. It needs <c>ManageB2BAccounts</c>.
/// </summary>
internal sealed class ListPriceListsTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPriceLists";

    private const int DefaultEntryCount = 100;

    private const int MaxEntryCount = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Text the price list's name contains. Leave it out to list every price list."
        },
        "priceListId": {
          "type": "string",
          "description": "The id of one price list, to get its prices by SKU instead of the list."
        },
        "sku": {
          "type": "string",
          "description": "With priceListId, only the prices whose SKU contains this text."
        },
        "page": {
          "type": "integer",
          "description": "The page of price lists, or of prices with priceListId, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many price lists per page, at most 50 (20 by default); with priceListId, how many prices, at most 500 (100 by default)."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists price lists with their percent off, currency and entry count, filtered by name; pass an id to get its SKU prices.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IPriceListStore>();
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var priceListId = ReadString(arguments, "priceListId");

        if (priceListId is not null)
        {
            var list = await store.FindByIdAsync(priceListId, cancellationToken);

            if (list is null)
            {
                return $"There is no price list with the id '{priceListId}'. Call listPriceLists without an id to find it.";
            }

            var size = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultEntryCount, 1, MaxEntryCount);
            var sku = ReadString(arguments, "sku");
            var entries = (list.Entries ?? [])
                .Where(entry => entry is not null && (sku is null || (entry.Sku ?? string.Empty).Contains(sku, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var shown = entries.Skip((page - 1) * size).Take(size).ToList();

            return Json(new
            {
                id = list.ItemId,
                name = list.Name,
                percentOff = list.PercentOff,
                currency = list.Currency,
                entryCount = list.Entries?.Count ?? 0,
                created = await LocalTimeAsync(services, list.CreatedUtc),
                modified = await LocalTimeAsync(services, list.ModifiedUtc),
                prices = new
                {
                    items = shown.Select(entry => new { sku = entry.Sku, unitPrice = entry.UnitPrice }).ToList(),
                    total = entries.Count,
                    page,
                    pageSize = size,
                    truncated = (page - 1) * size + shown.Count < entries.Count,
                },
            });
        }

        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var (count, lists) = await store.PageAsync(ReadString(arguments, "name"), (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var list in lists)
        {
            items.Add(new
            {
                id = list.ItemId,
                name = list.Name,
                percentOff = list.PercentOff,
                currency = list.Currency,
                entryCount = list.Entries?.Count ?? 0,
                modified = await LocalTimeAsync(services, list.ModifiedUtc ?? list.CreatedUtc),
            });
        }

        return Json(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + items.Count < count,
        });
    }
}
