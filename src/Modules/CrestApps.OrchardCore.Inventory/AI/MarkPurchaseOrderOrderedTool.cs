using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Marks a draft purchase order with at least one line as ordered, as the Mark ordered button of Commerce › Purchase
/// orders does. It records the status only: nothing is sent to the supplier. It needs
/// <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class MarkPurchaseOrderOrderedTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "markPurchaseOrderOrdered";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The draft purchase order, by id or number."
        }
      },
      "required": ["purchaseOrderId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a draft purchase order with at least one line as ordered, so its items can be received when they arrive. It records the status only and sends nothing to the supplier.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, ReadString(arguments, "purchaseOrderId"), cancellationToken);

        if (order is null)
        {
            return error;
        }

        if (!order.IsDraft)
        {
            return $"Nothing was changed: purchase order {order.Number} was already ordered (it is {StatusName(order.Status)}).";
        }

        if (order.Lines.Count == 0)
        {
            return $"Nothing was changed: add the items to order to {order.Number} first (savePurchaseOrder).";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        order.Status = PurchaseOrderStatus.Ordered;
        order.OrderedUtc = order.ModifiedUtc = services.GetRequiredService<IClock>().UtcNow;

        if (await CommitAsync(services, order, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(new
        {
            purchaseOrder = await SummarizeAsync(services, order, locations),
            message = $"Purchase order {order.Number} is marked ordered. Receive its items with receivePurchaseOrder when they arrive. Nothing was sent to the supplier.",
        });
    }
}
