using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Lists purchase orders as Commerce › Purchase orders does (<see cref="IPurchaseOrderStore.PageAsync"/>): the open
/// ones by default, or one status, or all, searched by the start of a number or a supplier's name, newest first. It
/// needs <see cref="InventoryPermissions.ViewInventory"/>.
/// </summary>
internal sealed class ListPurchaseOrdersTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPurchaseOrders";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "The start of a purchase order number or of a supplier's name."
        },
        "status": {
          "type": "string",
          "enum": ["open", "draft", "ordered", "partiallyReceived", "received", "canceled", "all"],
          "description": "Which purchase orders: open (the default: not received or canceled yet), one status, or all."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Purchase orders per page: 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists purchase orders with number, supplier, status, location, expected date, units ordered and received, and total; filters by text and status, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ViewInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status") ?? "open";
        var query = new PurchaseOrderQuery { Search = ReadString(arguments, "search") };

        if (string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query.All = true;
        }
        else if (Statuses.TryGetValue(status, out var chosen))
        {
            query.Status = chosen;
        }
        else if (!string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was listed: status must be open, draft, ordered, partiallyReceived, received, canceled or all.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (total, orders) = await services.GetRequiredService<IPurchaseOrderStore>().PageAsync(query, (page - 1) * pageSize, pageSize, cancellationToken);
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var items = new List<object>();

        foreach (var order in orders)
        {
            items.Add(await SummarizeAsync(services, order, locations));
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = total > page * pageSize,
        });
    }
}
