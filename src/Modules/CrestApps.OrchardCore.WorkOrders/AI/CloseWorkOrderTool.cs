using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Cancels an open work order or closes a completed one, as the Status form of its page does. Both are final.
/// </summary>
internal sealed class CloseWorkOrderTool : WorkOrderToolBase
{
    public const string ToolName = "closeWorkOrder";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." },
        "action": { "type": "string", "enum": ["Cancel", "Close"], "description": "Cancel an open work order, or Close a completed one." },
        "reason": { "type": "string", "description": "Why. Required to cancel; kept as the cancel reason and added to the history." }
      },
      "required": ["number", "action"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Cancels an open work order (a reason is required) or closes a completed one. Both are final: the work order cannot be reopened. With Dispatch on, cancelling also cancels its open visits, which notifies the customer when customer notices are on.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        var action = WorkOrderToolSupport.Read(arguments, "action");
        WorkOrderStatus status;

        if (string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            status = WorkOrderStatus.Canceled;
        }
        else if (string.Equals(action, "Close", StringComparison.OrdinalIgnoreCase))
        {
            status = WorkOrderStatus.Closed;
        }
        else
        {
            return "The action must be Cancel or Close.";
        }

        var previous = workOrder.Status;
        var result = await services.GetRequiredService<WorkOrderService>().ChangeStatusAsync(workOrder.ItemId, status, null, WorkOrderToolSupport.Read(arguments, "reason"), user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            WorkOrderToolSupport.Forget(services, workOrder);

            return "The work order was not closed: " + string.Join(" ", result.Errors);
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return WorkOrderToolSupport.Json(new
        {
            id = result.WorkOrder.ItemId,
            number = result.WorkOrder.Number,
            previousStatus = previous.ToString(),
            status = result.WorkOrder.Status.ToString(),
            cancelReason = result.WorkOrder.Status == WorkOrderStatus.Canceled ? result.WorkOrder.CancelReason : null,
            closed = await WorkOrderToolSupport.LocalAsync(services, result.WorkOrder.ClosedUtc),
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, result.WorkOrder.ItemId),
        });
    }
}
