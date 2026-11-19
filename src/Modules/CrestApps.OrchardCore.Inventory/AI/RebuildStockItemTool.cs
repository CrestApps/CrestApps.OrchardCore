using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Recomputes a stock item's units on hand from its movements, as the Recount button of its Inventory page does
/// (<see cref="IInventoryService.RebuildAsync"/>), committed at once. It needs
/// <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class RebuildStockItemTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "rebuildStockItem";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "stockItemId": {
          "type": "string",
          "description": "The stock item's id, from listStockItems or getStockItem."
        }
      },
      "required": ["stockItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Recomputes one stock item's units on hand from its movement history. Use it only when the stored count is in doubt.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (item, error) = await FindItemAsync(services, ReadString(arguments, "stockItemId"), cancellationToken);

        if (item is null)
        {
            return error;
        }

        var before = item.OnHand;
        var inventory = services.GetRequiredService<IInventoryService>();
        var (result, changeError) = await ChangeStockAsync(services, () => inventory.RebuildAsync(item.ItemId, cancellationToken), cancellationToken);

        if (result is null)
        {
            return changeError;
        }

        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();

        return Json(new
        {
            item = Describe(result.Item, locations),
            onHandBefore = before,
            message = $"Recounted from the history: {result.Item.OnHand} on hand{(result.Item.OnHand == before ? ", as stored." : $", where {before} was stored.")}",
        });
    }
}
