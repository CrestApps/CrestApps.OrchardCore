using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Adds a stock location, or changes one's name, address or place in the order, through the location list of Settings ›
/// Commerce › Inventory (<see cref="StockLocationEditor"/>): every location needs a name, at most
/// <see cref="StockLocationEditor.MaxLocations"/> are listed, a location keeps its id, and the positions are numbered
/// again in the new order. A location owned by another record, such as a van, is managed with its owner and is refused.
/// It needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class SaveStockLocationTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveStockLocation";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "locationId": {
          "type": "string",
          "description": "The location's id, from listStockLocations, to change it. Leave it out to add a location."
        },
        "name": {
          "type": "string",
          "description": "The location's name, such as Main warehouse. Required for a new location; a change keeps the name when left out."
        },
        "address": {
          "type": ["string", "null"],
          "description": "Where the location is, on one line, for staff. null clears it; a change keeps it when left out."
        },
        "position": {
          "type": "integer",
          "description": "Its place in the order orders take units from the locations, from 1. A new location goes last and a change keeps its place when left out."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a stock location or changes its name, address or place in the order orders take units from. A location keeps its id because stock levels and movements point at it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var locationId = ReadString(arguments, "locationId");
        var name = ReadString(arguments, "name");
        var position = ReadInt(arguments, "position");
        var rows = await StockLocationToolEditing.GetRowsAsync(services);
        InventoryLocationViewModel row;

        if (locationId is null)
        {
            if (name is null)
            {
                return "Nothing was saved: give the location a name, such as Main warehouse.";
            }

            if (rows.Count >= StockLocationEditor.MaxLocations)
            {
                return $"Nothing was saved: the store lists {StockLocationEditor.MaxLocations} locations, the most it can. Remove one first.";
            }

            row = new InventoryLocationViewModel { LocationId = UniqueId.GenerateId() };
            rows.Add(row);
        }
        else
        {
            row = rows.FirstOrDefault(entry => string.Equals(entry.LocationId, locationId, StringComparison.Ordinal));

            if (row is null)
            {
                var all = await services.GetRequiredService<IInventoryLocationService>().GetLocationsAsync();
                var owned = all.FirstOrDefault(entry => string.Equals(entry.LocationId, locationId, StringComparison.Ordinal));

                return owned is null
                    ? $"Nothing was saved: there is no stock location with the id '{locationId}'. Use listStockLocations for the ids."
                    : $"Nothing was saved: the location '{owned.Name}' belongs to a {owned.OwnerKind} and is managed with it.";
            }
        }

        if (name is not null)
        {
            row.Name = name;
        }

        if (arguments.ContainsKey("address"))
        {
            row.Address = ReadString(arguments, "address");
        }

        if (position is not null)
        {
            if (position < 1 || position > rows.Count)
            {
                return $"Nothing was saved: position must be from 1 to {rows.Count}.";
            }

            rows.Remove(row);
            rows.Insert(position.Value - 1, row);
        }

        var saved = await StockLocationToolEditing.SaveAsync(services, rows);

        if (saved is null)
        {
            return "Nothing was saved: every location needs a name, such as Main warehouse.";
        }

        return Json(new
        {
            locationId = row.LocationId,
            name = row.Name,
            address = row.Address,
            position = rows.IndexOf(row) + 1,
            created = locationId is null,
            order = saved.Select(location => new { locationId = location.LocationId, name = location.Name }).ToArray(),
            message = locationId is null ? $"The location {row.Name} was added." : $"The location {row.Name} was saved.",
        });
    }
}
