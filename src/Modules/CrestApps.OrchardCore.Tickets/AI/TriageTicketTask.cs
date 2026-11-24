using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// Triages a ticket with its desk's AI assistant (<see cref="TicketAssistant.TriageAsync"/>), such as when it is
/// created; the note's text is the workflow's <c>Triage</c> output.
/// </summary>
public sealed class TriageTicketTask : TicketTaskActivity
{
    public TriageTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<TriageTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    /// <inheritdoc/>
    public override string Name => nameof(TriageTicketTask);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Triage Ticket With AI"];

    /// <inheritdoc/>
    public override IReadOnlyList<TicketTaskField> Fields => [];

    /// <inheritdoc/>
    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var result = await Services.GetRequiredService<TicketAssistant>().TriageAsync(await tickets.GetAsync(ticketId));

        if (!result.Succeeded)
        {
            workflowContext.LastResult = result.Error;

            return Outcome("Failed");
        }

        workflowContext.Output["Triage"] = result.Text;

        return Outcome("Done");
    }
}
