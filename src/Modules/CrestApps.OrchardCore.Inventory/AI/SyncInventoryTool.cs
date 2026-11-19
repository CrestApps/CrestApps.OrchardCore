using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Starts counting the products whose type gained the Inventory part, and refreshes every stock item's title, SKU and
/// threshold, as the Sync button of Commerce › Inventory does (<see cref="InventorySyncService.SyncAllAsync"/>). It needs
/// <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class SyncInventoryTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "syncInventory";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts counting products whose type gained the Inventory part and refreshes every stock item's title, SKU and threshold from its product; returns how many products were checked. Use it after adding the Inventory part to a type or when stock items look stale.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (services.GetService<InventoryToolScope>()?.SessionClosed == true)
        {
            return "Nothing was changed: an earlier change in this message was refused, so nothing more can be saved in it. Ask again in your next message.";
        }

        var count = await services.GetRequiredService<InventorySyncService>().SyncAllAsync(cancellationToken);

        return Json(new
        {
            productsChecked = count,
            message = $"{count} products were checked for stock to count.",
        });
    }
}
