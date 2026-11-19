using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Removes a stock location from the list of Settings › Commerce › Inventory (<see cref="StockLocationEditor"/>). The
/// default location is never removed, and a location owned by another record, such as a van, is managed with its owner.
/// Units still at a removed location stay on their items, where they can be sold or transferred out. It needs
/// <see cref="InventoryPermissions.ManageInventory"/> and asks for approval.
/// </summary>
internal sealed class DeleteStockLocationTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteStockLocation";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "locationId": {
          "type": "string",
          "description": "The location's id, from listStockLocations."
        }
      },
      "required": ["locationId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a stock location from the list. The default location cannot be removed; units still at a removed location stay on their items and can be sold or transferred out.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var locationId = ReadString(arguments, "locationId");

        if (locationId == InventoryConstants.DefaultLocationId)
        {
            return "Nothing was removed: the default location cannot be removed. Stock counted before there were locations is there; rename it instead.";
        }

        var rows = await StockLocationToolEditing.GetRowsAsync(services);
        var row = rows.FirstOrDefault(entry => string.Equals(entry.LocationId, locationId, StringComparison.Ordinal));

        if (row is null)
        {
            var all = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
            var owned = all.FirstOrDefault(entry => string.Equals(entry.LocationId, locationId, StringComparison.Ordinal));

            return owned is null
                ? $"Nothing was removed: there is no stock location with the id '{locationId}'. Use listStockLocations for the ids."
                : $"Nothing was removed: the location '{owned.Name}' belongs to a {owned.OwnerKind} and is managed with it.";
        }

        rows.Remove(row);

        if (await StockLocationToolEditing.SaveAsync(services, rows) is null)
        {
            return "Nothing was removed: another location has no name. Name it on the Inventory settings page first.";
        }

        return Json(new
        {
            locationId,
            name = row.Name,
            removed = true,
            message = $"The location {row.Name} was removed. Units still there stay on their items and can be sold or transferred out.",
        });
    }
}
