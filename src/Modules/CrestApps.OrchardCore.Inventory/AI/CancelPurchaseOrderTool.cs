using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Cancels a purchase order that is not received or canceled, as the Cancel button of Commerce › Purchase orders does:
/// what already arrived stays in stock, and only what is still to come is canceled. It asks for approval and needs
/// <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class CancelPurchaseOrderTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelPurchaseOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The purchase order to cancel, by id or number."
        }
      },
      "required": ["purchaseOrderId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels a purchase order that is not fully received. What already arrived stays in stock; a canceled order cannot be changed or reopened.";

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

        if (!PurchaseOrderRules.CanChange(order))
        {
            return $"Nothing was changed: purchase order {order.Number} is already {StatusName(order.Status)}.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        // What was already received stays in stock; only what's still to come is canceled.
        order.Status = PurchaseOrderStatus.Canceled;
        order.ModifiedUtc = services.GetRequiredService<IClock>().UtcNow;

        if (await CommitAsync(services, order, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(new
        {
            purchaseOrder = await SummarizeAsync(services, order, locations),
            message = order.Received > 0
                ? $"Purchase order {order.Number} is canceled. The {order.Received} units already received stay in stock."
                : $"Purchase order {order.Number} is canceled.",
        });
    }
}
