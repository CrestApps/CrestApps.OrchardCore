using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Deletes a draft purchase order, as the Delete button of Commerce › Purchase orders does. A purchase order already
/// ordered is a record of what was asked of the supplier: it can be canceled, not deleted. It asks for approval and
/// needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class DeletePurchaseOrderTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deletePurchaseOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The draft purchase order to delete, by id or number."
        }
      },
      "required": ["purchaseOrderId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a draft purchase order. An order already sent to a supplier cannot be deleted, only canceled.";

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

        // An order that was sent to a supplier is a record of it: it can be canceled, not deleted.
        if (!order.IsDraft)
        {
            return $"Nothing was deleted: purchase order {order.Number} is {StatusName(order.Status)}. Only a draft can be deleted; cancel it instead (cancelPurchaseOrder).";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        await services.GetRequiredService<IPurchaseOrderStore>().DeleteAsync(order, cancellationToken);

        if (await CommitAsync(services, order: null, cancellationToken) is { } commitError)
        {
            return commitError;
        }

        return Json(new
        {
            deleted = new
            {
                purchaseOrderId = order.ItemId,
                number = order.Number,
            },
            message = $"Draft {order.Number} deleted.",
        });
    }
}
