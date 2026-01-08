using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;

public sealed class SubscriptionFailedEvent : EventActivity
{
    public const string EventName = "SubscriptionFailedEvent";

    internal readonly IStringLocalizer S;

    public SubscriptionFailedEvent(IStringLocalizer<SubscriptionFailedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name
        => EventName;

    public override LocalizedString DisplayText
        => S["Subscription Failed Event"];

    public override LocalizedString Category
        => S["Subscriptions"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"]);
    }

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes("Done");
    }
}
