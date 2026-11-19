using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Returns one purchase order with its details and lines, as its page on Commerce › Purchase orders shows it. It needs
/// <see cref="InventoryPermissions.ViewInventory"/>.
/// </summary>
internal sealed class GetPurchaseOrderTool : PurchaseOrderAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getPurchaseOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "purchaseOrderId": {
          "type": "string",
          "description": "The purchase order's id, or its number such as PO-261019-K3QZ7, from listPurchaseOrders."
        }
      },
      "required": ["purchaseOrderId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one purchase order with its details and lines (stock item, SKU, ordered, received, outstanding, unit cost). Use it before changing or receiving it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ViewInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, ReadString(arguments, "purchaseOrderId"), cancellationToken);

        if (order is null)
        {
            return error;
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(await DescribeAsync(services, order, locations));
    }
}
