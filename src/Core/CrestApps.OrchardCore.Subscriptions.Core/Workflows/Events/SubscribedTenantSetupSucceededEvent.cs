using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;

public sealed class SubscribedTenantSetupSucceededEvent : EventActivity
{
    public const string EventName = "SubscribedTenantSetupSucceededEvent";

    internal readonly IStringLocalizer S;

    public SubscribedTenantSetupSucceededEvent(IStringLocalizer<SubscribedTenantSetupSucceededEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name
        => EventName;

    public override LocalizedString DisplayText
        => S["Subscribed Tenant Setup Succeeded Event"];

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
