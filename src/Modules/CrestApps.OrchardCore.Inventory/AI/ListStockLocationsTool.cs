using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Lists the stock locations (<see cref="IInventoryLocationService.GetLocationsAsync"/>) in the order orders take units
/// from them: the site's own first, then those owned by another record, such as a van, which are managed with their
/// owner. It needs <see cref="InventoryPermissions.ViewInventory"/>.
/// </summary>
internal sealed class ListStockLocationsTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listStockLocations";

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
    public override string Description => "Lists the store's stock locations in the order orders take units from them, with id, name and address. Use it to get a location id for stock changes, purchase orders, returns and registers.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ViewInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var locations = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
        var position = 0;

        return Json(new
        {
            items = locations.Select(location => new
            {
                locationId = location.LocationId,
                name = location.Name,
                address = location.Address,
                position = location.OwnerKind is null ? ++position : (int?)null,
                isDefault = location.LocationId == InventoryConstants.DefaultLocationId ? true : (bool?)null,
                ownedBy = location.OwnerKind,
                ownerId = location.OwnerId,
            }).ToArray(),
            total = locations.Count,
        });
    }
}
