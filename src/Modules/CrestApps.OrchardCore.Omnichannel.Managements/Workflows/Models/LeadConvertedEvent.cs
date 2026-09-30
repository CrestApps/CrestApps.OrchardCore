using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;

/// <summary>
/// A workflow event that starts when a lead is converted, however the conversion was started: the Convert screen, a
/// Convert Lead subject action or the Convert Lead task. The workflow input carries the lead, the contact it became
/// (as <c>ContentItem</c>, so content tasks act on the contact) and the ids of the account and opportunity.
/// </summary>
public sealed class LeadConvertedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadConvertedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadConvertedEvent(IStringLocalizer<LeadConvertedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(LeadConvertedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Lead Converted"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Omnichannel CRM"];

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
