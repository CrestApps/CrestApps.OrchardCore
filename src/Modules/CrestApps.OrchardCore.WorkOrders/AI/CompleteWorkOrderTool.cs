using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Completes a work order, as the Complete button of its page does: it checks the required answers, signature and
/// photos, and bills the work order when its type is billable and a billing feature is on.
/// </summary>
internal sealed class CompleteWorkOrderTool : WorkOrderToolBase
{
    public const string ToolName = "completeWorkOrder";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." }
      },
      "required": ["number"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Completes a work order in progress once its required answers, signature and photos are in, and creates the customer's invoice when the type is billable and billing is on. It returns what is still missing instead when it cannot complete.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        var service = services.GetRequiredService<WorkOrderService>();
        var alreadyBilled = !string.IsNullOrEmpty(workOrder.InvoiceTransactionId);
        var result = await service.CompleteAsync(workOrder.ItemId, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            WorkOrderToolSupport.Forget(services, workOrder);

            return WorkOrderToolSupport.Json(new
            {
                completed = false,
                number = workOrder.Number,
                status = workOrder.Status.ToString(),
                missing = result.Errors,
            });
        }

        if (await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var type = string.IsNullOrEmpty(result.WorkOrder.TypeId) ? null : await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().FindByIdAsync(result.WorkOrder.TypeId, cancellationToken);
        var billable = type is not null && (type.BillingMode != WorkOrderBillingMode.NotBillable || result.WorkOrder.AgreedPrice is not null);
        var amount = type is null ? 0 : WorkOrderService.BillableAmount(result.WorkOrder, type);

        // Billing ran when the type bills, the amount is above zero, a billing feature is on, and nothing billed it before.
        var billingRequested = billable && amount > 0 && service.Bills && !alreadyBilled;

        return WorkOrderToolSupport.Json(new
        {
            completed = true,
            id = result.WorkOrder.ItemId,
            number = result.WorkOrder.Number,
            status = result.WorkOrder.Status.ToString(),
            completedAt = await WorkOrderToolSupport.LocalAsync(services, result.WorkOrder.CompletedUtc),
            billableAmount = billable ? amount : 0,
            currency = type?.Currency ?? "USD",
            billingRequested,
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, result.WorkOrder.ItemId),
        });
    }
}
