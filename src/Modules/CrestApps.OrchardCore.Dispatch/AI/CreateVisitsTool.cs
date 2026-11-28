using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Makes a visit for one work order, as the work order page's "Add a visit" does, or one for every open work order that
/// has none, as the Visits page's "Make missing visits" does.
/// </summary>
internal sealed class CreateVisitsTool : DispatchAdminToolBase
{
    public const string ToolName = "createVisits";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workOrderNumber": { "type": "string", "description": "The work order's number (WO-000012) to add one more visit to. Leave out with allMissing." },
        "allMissing": { "type": "boolean", "description": "True to make a visit for every open work order that has none. Leave out with workOrderNumber." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Makes a visit for one work order, or for every open work order that has none. New visits start unscheduled; give them a technician and a time with dispatchRescheduleVisit or on the board.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ManageDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var number = DispatchAdminTools.Read(arguments, "workOrderNumber");
        var allMissing = DispatchAdminTools.ReadBool(arguments, "allMissing") == true;

        if ((number is null) == !allMissing)
        {
            return "Give either a work order's number (workOrderNumber) or allMissing: true, not both.";
        }

        var dispatch = services.GetRequiredService<IDispatchService>();
        var actor = user.Identity?.Name;

        if (allMissing)
        {
            var made = await dispatch.CreateMissingAsync(actor, cancellationToken);

            await DispatchAdminTools.SaveAsync(services, cancellationToken);

            return DispatchAdminTools.Json(new { created = made, note = made == 0 ? "Every open work order already has a visit." : "The new visits are unscheduled." });
        }

        var workOrder = await FieldServiceLookups.WorkOrderAsync(services, number, cancellationToken);

        if (workOrder is null)
        {
            return $"No work order is numbered '{number}'.";
        }

        var result = await dispatch.CreateAsync(workOrder, actor, cancellationToken);

        if (!result.Succeeded)
        {
            return $"No visit was made: {string.Join(" ", result.Errors)}";
        }

        await DispatchAdminTools.SaveAsync(services, cancellationToken);

        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var names = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);

        return DispatchAdminTools.Json(new
        {
            created = 1,
            workOrderNumber = workOrder.Number,
            visit = DispatchAdminTools.Changed(result.Visit, names, zone),
        });
    }
}
