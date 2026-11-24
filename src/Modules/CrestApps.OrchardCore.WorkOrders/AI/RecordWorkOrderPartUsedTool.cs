using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Records what was used on a work order's line, as the Use form of its page does, taking parts off a stock location
/// when Inventory is on.
/// </summary>
internal sealed class RecordWorkOrderPartUsedTool : WorkOrderToolBase
{
    public const string ToolName = "recordWorkOrderPartUsed";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." },
        "line": { "type": "string", "description": "The line's ID, or its exact description. When it matches no single line, the work order's lines are returned to pick from." },
        "quantity": { "type": "number", "exclusiveMinimum": 0, "description": "How many were used, such as 2 filters or 1.5 hours; added to what the line already used." },
        "locationId": { "type": "string", "description": "The Inventory stock location the parts came from, such as the technician's van. Needed for a product line taken from stock; the locations are returned when it is missing." }
      },
      "required": ["number", "line", "quantity"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Records the quantity used of a product, labor or expense line of a work order, taken from a stock location when Inventory is on. Only an open work order takes parts.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        var reference = WorkOrderToolSupport.Read(arguments, "line");
        var line = workOrder.Lines.FirstOrDefault(item => item.Id == reference);

        if (line is null && reference is not null)
        {
            var matches = workOrder.Lines.Where(item => string.Equals(item.Description?.Trim(), reference, StringComparison.OrdinalIgnoreCase)).ToList();

            line = matches.Count == 1 ? matches[0] : null;
        }

        if (line is null)
        {
            return WorkOrderToolSupport.Json(new
            {
                error = $"No single line of {workOrder.Number} matches '{reference}'. Pick one of these by its id.",
                lines = WorkOrderToolResults.Lines(workOrder),
            });
        }

        if (WorkOrderToolSupport.ReadDecimal(arguments, "quantity") is not { } quantity || quantity <= 0)
        {
            return "The quantity must be a number above 0.";
        }

        var service = services.GetRequiredService<WorkOrderService>();
        var locationId = WorkOrderToolSupport.Read(arguments, "locationId");
        var takesStock = line.Kind == WorkOrderLineKind.Product && !string.IsNullOrEmpty(line.StockItemId);

        if (takesStock && service.TracksParts)
        {
            var locations = await services.GetRequiredService<WorkOrderLookups>().LocationsAsync(cancellationToken);

            if (locationId is null || !locations.Any(location => location.Value == locationId))
            {
                return WorkOrderToolSupport.Json(new
                {
                    error = locationId is null
                        ? "Say which stock location the parts came from."
                        : $"No stock location has the ID '{locationId}'. Pick one of these.",
                    locations = locations.Select(location => new { id = location.Value, name = location.Text }),
                });
            }
        }

        var result = await service.UsePartAsync(workOrder.ItemId, line.Id, quantity, locationId, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            WorkOrderToolSupport.Forget(services, workOrder);

            return "Nothing was recorded: " + string.Join(" ", result.Errors);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            id = result.WorkOrder.ItemId,
            number = result.WorkOrder.Number,
            recorded = quantity.ToString(CultureInfo.InvariantCulture) + " × " + line.Description,
            takenFromStock = takesStock && service.TracksParts,
            line = WorkOrderToolResults.Lines(result.WorkOrder).ElementAt(result.WorkOrder.Lines.FindIndex(item => item.Id == line.Id)),
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, result.WorkOrder.ItemId),
        });
    }
}
