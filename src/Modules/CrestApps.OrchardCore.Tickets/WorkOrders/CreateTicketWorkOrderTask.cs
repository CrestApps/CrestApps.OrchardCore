using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tickets.WorkOrders;

/// <summary>
/// Creates a work order from a ticket (<see cref="TicketWorkOrders"/>); the work order's id and number are the
/// workflow's <c>WorkOrderId</c> and <c>WorkOrderNumber</c> outputs.
/// </summary>
public sealed class CreateTicketWorkOrderTask : TicketTaskActivity
{
    public CreateTicketWorkOrderTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<CreateTicketWorkOrderTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    /// <inheritdoc/>
    public override string Name => nameof(CreateTicketWorkOrderTask);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Create Work Order From Ticket"];

    /// <inheritdoc/>
    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Type", S["Work order type"], S["Liquid giving the type's name or id."], TicketTaskFieldKind.Liquid),
        new("Priority", S["Priority"], S["Leave empty for the type's."], TicketTaskFieldKind.Choice,
            [
                new(nameof(WorkOrderPriority.Low), S["Low"]),
                new(nameof(WorkOrderPriority.Normal), S["Normal"]),
                new(nameof(WorkOrderPriority.High), S["High"]),
                new(nameof(WorkOrderPriority.Urgent), S["Urgent"]),
            ]),
    ];

    /// <inheritdoc/>
    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var type = await FieldAsync("Type", workflowContext);
        var types = Services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
        var typeId = string.IsNullOrEmpty(type)
            ? null
            : (await types.FindByIdAsync(type))?.ItemId
                ?? (await types.GetAllAsync()).FirstOrDefault(item => string.Equals(item.Name, type, StringComparison.OrdinalIgnoreCase))?.ItemId;
        var priority = Enum.TryParse<WorkOrderPriority>(GetValue("Priority"), out var chosen) ? chosen : (WorkOrderPriority?)null;

        var result = await Services.GetRequiredService<TicketWorkOrders>().CreateAsync(ticketId, typeId, priority, Actor);

        if (result.WorkOrder is not null)
        {
            workflowContext.Output["WorkOrderId"] = result.WorkOrder.ItemId;
            workflowContext.Output["WorkOrderNumber"] = result.WorkOrder.Number;
        }

        if (result.Succeeded)
        {
            return Outcome("Done");
        }

        workflowContext.LastResult = string.Join(" ", result.Errors);

        return Outcome("Failed");
    }
}
