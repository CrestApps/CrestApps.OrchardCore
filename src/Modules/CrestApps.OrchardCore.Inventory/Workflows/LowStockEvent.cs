using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Inventory.Workflows;

/// <summary>
/// A workflow event that starts when a product, or one of its variants, runs low or sells out. The input carries the
/// product (as <c>ContentItem</c>, so content tasks act on it), the stock item, and its SKU, title, units available,
/// threshold and whether it sold out.
/// </summary>
public sealed class LowStockEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    public LowStockEvent(IStringLocalizer<LowStockEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name => nameof(LowStockEvent);

    public override LocalizedString DisplayText => S["Low Stock"];

    public override LocalizedString Category => S["Commerce"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
