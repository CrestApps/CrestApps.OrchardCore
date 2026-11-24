using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Pins or unpins a visit, and takes it off its technicians, as the visit page's Pin, Unpin and Unassign buttons do. None
/// of these sends the customer a notice.
/// </summary>
internal sealed class UpdateVisitTool : DispatchAdminToolBase
{
    public const string ToolName = "updateVisit";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "visit": { "type": "string", "description": "The visit's number (V-000012), its ID, or its work order's number for the work order's open visit." },
        "pinned": { "type": "boolean", "description": "Optional. True pins the visit so the optimizer leaves its technician and time alone; false unpins it." },
        "unassign": { "type": "boolean", "description": "Optional. True takes a scheduled visit that hasn't started off its technicians and time and puts it back with the unscheduled visits. A pinned visit is unpinned first only when pinned is false." }
      },
      "required": ["visit"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Changes a visit's place on the board: pins it so the optimizer leaves it alone, unpins it, or takes it off its technicians and time back to the unscheduled visits. To move it to another time or technician use dispatchRescheduleVisit.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ManageDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reference = DispatchAdminTools.Read(arguments, "visit");
        var pinned = DispatchAdminTools.ReadBool(arguments, "pinned");
        var unassign = DispatchAdminTools.ReadBool(arguments, "unassign") == true;

        if (reference is null)
        {
            return "Give the visit's number or ID.";
        }

        if (pinned is null && !unassign)
        {
            return "Nothing to change: give pinned (true or false) or unassign: true.";
        }

        if (pinned == true && unassign)
        {
            return "A visit taken off its technicians can't be pinned. Unassign it, then give it a time, then pin it.";
        }

        var visit = await DispatchAdminTools.FindVisitAsync(services, reference, cancellationToken);

        if (visit is null)
        {
            return $"No visit or work order is numbered '{reference}'. Use listVisits to find it.";
        }

        var dispatch = services.GetRequiredService<IDispatchService>();
        var actor = user.Identity?.Name;
        var changes = new List<string>();

        // Unpinned first, so a pinned visit can be taken off its technicians in the same call.
        if (pinned == false && visit.Pinned)
        {
            var result = await dispatch.PinAsync(visit.ItemId, false, actor, cancellationToken);

            if (!result.Succeeded)
            {
                return $"Nothing was changed: {string.Join(" ", result.Errors)}";
            }

            visit = result.Visit;
            changes.Add("unpinned");
        }

        if (unassign)
        {
            var result = await dispatch.UnassignAsync(visit.ItemId, actor, cancellationToken);

            if (!result.Succeeded)
            {
                if (changes.Count > 0)
                {
                    await DispatchAdminTools.SaveAsync(services, cancellationToken);
                }

                return DispatchAdminTools.Json(new
                {
                    changed = changes,
                    refused = $"The visit was not taken off its technicians: {string.Join(" ", result.Errors)}",
                });
            }

            visit = result.Visit;
            changes.Add("unassigned");
        }

        if (pinned == true && !visit.Pinned)
        {
            var result = await dispatch.PinAsync(visit.ItemId, true, actor, cancellationToken);

            if (!result.Succeeded)
            {
                return $"Nothing was changed: {string.Join(" ", result.Errors)}";
            }

            visit = result.Visit;
            changes.Add("pinned");
        }

        if (changes.Count > 0)
        {
            await DispatchAdminTools.SaveAsync(services, cancellationToken);
        }

        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var names = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);

        return DispatchAdminTools.Json(new
        {
            changed = changes,
            note = changes.Count == 0 ? "The visit was already as asked; nothing changed." : null,
            visit = DispatchAdminTools.Changed(visit, names, zone),
        });
    }
}
