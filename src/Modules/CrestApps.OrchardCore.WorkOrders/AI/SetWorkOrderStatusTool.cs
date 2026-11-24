using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Moves an open work order to another open status, as the Status form of its page does.
/// </summary>
internal sealed class SetWorkOrderStatusTool : WorkOrderToolBase
{
    public const string ToolName = "setWorkOrderStatus";

    private static readonly WorkOrderStatus[] _allowed = [WorkOrderStatus.Ready, WorkOrderStatus.Scheduled, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." },
        "status": { "type": "string", "enum": ["Ready", "Scheduled", "InProgress", "OnHold"], "description": "The status to move to." },
        "subStatus": { "type": "string", "description": "A sub-status under that status, such as Waiting for parts under OnHold, by its name or ID. Leave it out for none." },
        "note": { "type": "string", "description": "Why, added to the work order's history." }
      },
      "required": ["number", "status"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Moves an open work order to Ready, Scheduled, In progress or On hold, with an optional sub-status and note. Only the moves the current status allows are accepted. Use closeWorkOrder to cancel or close, and completeWorkOrder to complete.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        if (!WorkOrderToolSupport.TryParseEnum<WorkOrderStatus>(WorkOrderToolSupport.Read(arguments, "status"), out var status) || !_allowed.Contains(status))
        {
            return "The status must be Ready, Scheduled, InProgress or OnHold. Use closeWorkOrder to cancel or close a work order, and completeWorkOrder to complete it.";
        }

        string subStatusId = null;
        var subStatusText = WorkOrderToolSupport.Read(arguments, "subStatus");

        if (subStatusText is not null)
        {
            var candidates = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>().GetAllAsync(cancellationToken))
                .Where(item => item.ParentStatus == status)
                .ToList();
            var matches = candidates.Where(item => item.ItemId == subStatusText).ToList();

            if (matches.Count == 0)
            {
                matches = [.. candidates.Where(item => string.Equals(item.Name?.Trim(), subStatusText, StringComparison.OrdinalIgnoreCase))];
            }

            if (matches.Count != 1)
            {
                return WorkOrderToolSupport.Json(new
                {
                    error = $"No single sub-status of {status} matches '{subStatusText}'. Pick one of these, or leave the sub-status out.",
                    subStatuses = candidates.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new { id = item.ItemId, name = item.Name }),
                });
            }

            subStatusId = matches[0].ItemId;
        }

        var previous = workOrder.Status;
        var result = await services.GetRequiredService<WorkOrderService>().ChangeStatusAsync(workOrder.ItemId, status, subStatusId, WorkOrderToolSupport.Read(arguments, "note"), user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            WorkOrderToolSupport.Forget(services, workOrder);

            return "The status was not changed: " + string.Join(" ", result.Errors);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var (_, subStatuses) = await WorkOrderToolResults.NamesAsync(services, cancellationToken);

        return WorkOrderToolSupport.Json(new
        {
            id = result.WorkOrder.ItemId,
            number = result.WorkOrder.Number,
            previousStatus = previous.ToString(),
            status = result.WorkOrder.Status.ToString(),
            subStatus = subStatuses.GetValueOrDefault(result.WorkOrder.SubStatusId ?? string.Empty)?.Name,
            respondBy = await WorkOrderToolSupport.LocalAsync(services, result.WorkOrder.Sla?.RespondByUtc),
            resolveBy = await WorkOrderToolSupport.LocalAsync(services, result.WorkOrder.Sla?.ResolveByUtc),
            slaPaused = result.WorkOrder.Sla?.PausedSinceUtc is not null,
            timeZoneId = await WorkOrderToolSupport.GetTimeZoneIdAsync(services),
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, result.WorkOrder.ItemId),
        });
    }
}
