using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Creates a draft purchase order, or changes one not yet received or canceled, with the rules of Commerce › Purchase
/// orders (<see cref="PurchaseOrderRules"/>): a supplier is required, the number comes from the page's locked sequence, a
/// new order can start with the items low on stock at its location, a draft's lines can be added, removed and their
/// quantities changed, and once ordered only their costs. Stricter than the page: an unknown location, currency or stock
/// item is refused instead of being ignored, and a quantity change to an ordered purchase order is refused. It sends
/// nothing to the supplier, and needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class SavePurchaseOrderTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "savePurchaseOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The purchase order to change, by id or number. Leave it out to create a draft."
        },
        "supplier": {
          "type": "string",
          "description": "Who the stock is ordered from. Required for a new purchase order."
        },
        "supplierEmail": {
          "type": "string",
          "description": "The supplier's email, kept with the purchase order. Nothing is sent to it."
        },
        "locationId": {
          "type": "string",
          "description": "The stock location the items arrive at, from listStockLocations. A new purchase order uses the first listed location when left out. It cannot change once some stock has arrived."
        },
        "currency": {
          "type": "string",
          "description": "The currency of the unit costs, a code the store sells in such as USD. A new purchase order uses the store's first currency when left out."
        },
        "expectedDate": {
          "type": "string",
          "description": "When the stock is expected, as yyyy-MM-dd, or 'none' to clear it."
        },
        "note": {
          "type": "string",
          "description": "A note kept with the purchase order."
        },
        "startWithLowStock": {
          "type": "boolean",
          "description": "For a new purchase order only: start it with every item at or below its low-stock threshold at the location, each ordered up to twice its threshold."
        },
        "lines": {
          "type": "array",
          "description": "Lines to add or change. On a draft: add a stock item, or change a line's quantity or cost, or remove it. Once ordered: only a line's unit cost.",
          "items": {
            "type": "object",
            "properties": {
              "lineId": {
                "type": "string",
                "description": "The line to change, from getPurchaseOrder."
              },
              "stockItemId": {
                "type": "string",
                "description": "The stock item to add (or whose line to change), from listStockItems."
              },
              "quantity": {
                "type": "integer",
                "description": "The units to order, 1 to 1,000,000. A new line orders 1 when left out."
              },
              "unitCost": {
                "type": "number",
                "description": "What one unit costs from the supplier, in the purchase order's currency; zero or more."
              },
              "remove": {
                "type": "boolean",
                "description": "Remove the line from a draft."
              }
            },
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a draft purchase order (supplier, location, expected date, note, lines, or started from the items that are low on stock) or changes one that is not yet received or canceled. It does not send anything to the supplier.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        PurchaseOrder order = null;
        var purchaseOrderId = ReadString(arguments, "purchaseOrderId");

        if (purchaseOrderId is not null)
        {
            (order, var findError) = await FindOrderAsync(services, purchaseOrderId, cancellationToken);

            if (order is null)
            {
                return findError;
            }

            if (!PurchaseOrderRules.CanChange(order))
            {
                return $"Nothing was changed: purchase order {order.Number} is {StatusName(order.Status)}, and a purchase order that's received or canceled can't be changed.";
            }

            if (ReadJson(arguments, "startWithLowStock") is not null)
            {
                return "Nothing was changed: startWithLowStock is only for a new purchase order. Add stock items as lines instead.";
            }
        }

        var supplier = ReadString(arguments, "supplier");

        if (order is null && supplier is null)
        {
            return "Nothing was created: say who the stock is ordered from (supplier).";
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var locationId = ReadString(arguments, "locationId");

        if (locationId is not null)
        {
            if (!locations.Any(location => location.LocationId == locationId))
            {
                return $"Nothing was saved: there is no stock location with the id '{locationId}'. Use listStockLocations for the ids.";
            }

            if (order is not null && order.Received > 0 && order.LocationId != locationId)
            {
                return "Nothing was changed: some stock has already arrived at the purchase order's location, so the location can't change.";
            }
        }

        var currencies = await services.GetRequiredService<IProductCurrencyProvider>().GetCurrenciesAsync(cancellationToken);
        var currency = ReadString(arguments, "currency")?.ToUpperInvariant();

        if (currency is not null && currencies.Count > 0 && !currencies.Any(entry => string.Equals(entry.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was saved: the store doesn't sell in '{currency}'. Use one of: {string.Join(", ", currencies.Select(entry => entry.CurrencyCode))}.";
        }

        var expectedText = ReadString(arguments, "expectedDate");
        DateTime? expectedDate = order?.ExpectedUtc?.Date;

        if (expectedText is not null)
        {
            if (string.Equals(expectedText, "none", StringComparison.OrdinalIgnoreCase))
            {
                expectedDate = null;
            }
            else if (TryReadDate(expectedText, out var parsed))
            {
                expectedDate = parsed;
            }
            else
            {
                return "Nothing was saved: write the expected date as yyyy-MM-dd, or 'none' to clear it.";
            }
        }

        var (lines, linesError) = ReadLines(arguments);

        if (linesError is not null)
        {
            return linesError;
        }

        var items = services.GetRequiredService<IStockItemStore>();

        if (await CheckLinesAsync(order, lines, items, cancellationToken) is { } checkError)
        {
            return checkError;
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var now = services.GetRequiredService<IClock>().UtcNow;
        var isNew = order is null;

        order ??= new PurchaseOrder
        {
            ItemId = UniqueId.GenerateId(),
            Status = PurchaseOrderStatus.Draft,
            CreatedUtc = now,
            OwnerId = caller.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = caller.Identity?.Name,
        };

        PurchaseOrderRules.ApplyDetails(order, new PurchaseOrderDetails
        {
            Supplier = supplier ?? order.Supplier,
            SupplierEmail = ReadString(arguments, "supplierEmail") ?? order.SupplierEmail,
            LocationId = locationId ?? (isNew ? locations[0].LocationId : order.LocationId),
            Currency = currency ?? order.Currency ?? (currencies.Count > 0 ? currencies[0].CurrencyCode : null),
            ExpectedDate = expectedDate,
            Note = ReadString(arguments, "note") ?? order.Note,
        }, locations);

        var startedWithLowStock = 0;

        if (isNew)
        {
            order.Number = await PurchaseOrderRules.NewNumberAsync(services.GetRequiredService<INumberSequence>(), now, cancellationToken);

            if (ReadJson(arguments, "startWithLowStock") is { ValueKind: JsonValueKind.True })
            {
                order.Lines = await PurchaseOrderRules.LowStockLinesAsync(items, order.LocationId, cancellationToken);
                startedWithLowStock = order.Lines.Count;
            }
        }

        await ApplyLinesAsync(order, lines, items, cancellationToken);

        order.ModifiedUtc = now;

        if (isNew)
        {
            await services.GetRequiredService<IPurchaseOrderStore>().CreateAsync(order, cancellationToken);
        }

        if (await CommitAsync(services, isNew ? null : order, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            purchaseOrder = await DescribeAsync(services, order, locations),
            message = isNew
                ? startedWithLowStock > 0
                    ? $"Purchase order {order.Number} started as a draft with the {startedWithLowStock} items that are low on stock. Check the quantities, then mark it ordered."
                    : $"Purchase order {order.Number} started as a draft. Nothing was sent to the supplier."
                : $"Purchase order {order.Number} saved. Nothing was sent to the supplier.",
        });
    }

    // The lines given, each a line to change (by line id) or a stock item to add or whose line to change.
    private static (List<LineArgument> Lines, string Error) ReadLines(AIFunctionArguments arguments)
    {
        var lines = new List<LineArgument>();

        if (ReadJson(arguments, "lines") is not { } element)
        {
            return (lines, null);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return (null, "Nothing was saved: lines must be a list.");
        }

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                return (null, "Nothing was saved: each line must be an object with lineId or stockItemId.");
            }

            var line = new LineArgument
            {
                LineId = entry.TryGetProperty("lineId", out var lineId) ? ToText(lineId) : null,
                StockItemId = entry.TryGetProperty("stockItemId", out var stockItemId) ? ToText(stockItemId) : null,
                Quantity = entry.TryGetProperty("quantity", out var quantity) && quantity.ValueKind != JsonValueKind.Null ? ToInt(quantity) ?? int.MinValue : null,
                Remove = entry.TryGetProperty("remove", out var remove) && remove.ValueKind == JsonValueKind.True,
            };

            if (entry.TryGetProperty("unitCost", out var unitCost) && unitCost.ValueKind != JsonValueKind.Null)
            {
                if (unitCost.ValueKind != JsonValueKind.Number || !unitCost.TryGetDecimal(out var cost) || cost < 0)
                {
                    return (null, "Nothing was saved: a unit cost is a number, zero or more.");
                }

                line.UnitCost = cost;
            }

            if (line.LineId is null && line.StockItemId is null)
            {
                return (null, "Nothing was saved: each line needs a lineId (to change it) or a stockItemId (to add it).");
            }

            if (line.Quantity is { } ordered && ordered is not (> 0 and <= PurchaseOrderRules.MaxQuantity))
            {
                return (null, "Nothing was saved: a quantity is a whole number from 1 to 1,000,000. Use remove to take a line off a draft.");
            }

            lines.Add(line);
        }

        return (lines, null);
    }

    // Refuses, before anything changes, what the page would quietly ignore: a stock item that doesn't exist, a line
    // that isn't on the purchase order, too many lines, and quantity changes once it is ordered.
    private static async Task<string> CheckLinesAsync(PurchaseOrder order, List<LineArgument> lines, IStockItemStore items, CancellationToken cancellationToken)
    {
        var isDraft = order?.IsDraft ?? true;
        var adding = 0;

        foreach (var line in lines)
        {
            if (line.LineId is not null)
            {
                if (order?.Lines.Any(existing => existing.LineId == line.LineId) != true)
                {
                    return $"Nothing was saved: the purchase order has no line '{line.LineId}'. Use getPurchaseOrder for its line ids.";
                }
            }
            else if (order?.Lines.Any(existing => existing.StockItemId == line.StockItemId) != true)
            {
                if (!isDraft)
                {
                    return "Nothing was changed: items can be added only while the purchase order is a draft.";
                }

                if (line.Remove)
                {
                    return $"Nothing was saved: stock item '{line.StockItemId}' is not on the purchase order, so it can't be removed.";
                }

                if (await items.FindByIdAsync(line.StockItemId, cancellationToken) is not { Archived: false })
                {
                    return $"Nothing was saved: no stock item has the id '{line.StockItemId}', or it is archived. Use listStockItems to find it.";
                }

                adding++;
            }

            if (!isDraft && (line.Quantity is not null || line.Remove))
            {
                return "Nothing was changed: once a purchase order is ordered, only its lines' unit costs can change. Cancel it and start another to order different quantities.";
            }
        }

        // A new order starting with the low-stock items gets them first; the rules stop adding at the limit.
        if ((order?.Lines.Count ?? 0) + adding > PurchaseOrderRules.MaxLines)
        {
            return $"Nothing was saved: a purchase order has at most {PurchaseOrderRules.MaxLines} lines.";
        }

        return null;
    }

    private static async Task ApplyLinesAsync(PurchaseOrder order, List<LineArgument> lines, IStockItemStore items, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return;
        }

        // New stock items are added first, with one unit, then every line given gets its quantity and cost.
        await PurchaseOrderRules.ApplyLinesAsync(order, [], lines.Where(line => line.LineId is null).Select(line => line.StockItemId), items, cancellationToken);

        var changes = new List<PurchaseOrderLineChange>();

        foreach (var line in lines)
        {
            var existing = line.LineId is not null
                ? order.Lines.FirstOrDefault(entry => entry.LineId == line.LineId)
                : order.Lines.FirstOrDefault(entry => entry.StockItemId == line.StockItemId);

            if (existing is not null)
            {
                changes.Add(new PurchaseOrderLineChange(existing.LineId, line.Quantity ?? existing.Ordered, line.UnitCost ?? existing.UnitCost, line.Remove));
            }
        }

        await PurchaseOrderRules.ApplyLinesAsync(order, changes, [], items, cancellationToken);
    }

    private sealed class LineArgument
    {
        public string LineId { get; init; }

        public string StockItemId { get; init; }

        public int? Quantity { get; init; }

        public decimal? UnitCost { get; set; }

        public bool Remove { get; init; }
    }
}
