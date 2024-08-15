using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;

public sealed class SubscribedTenantFailedSetupEvent : EventActivity
{
    public const string EventName = "SubscribedTenantFailedSetupEvent";

    internal readonly IStringLocalizer S;

    public SubscribedTenantFailedSetupEvent(IStringLocalizer<SubscribedTenantFailedSetupEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override string Name
        => EventName;

    public override LocalizedString DisplayText
        => S["Subscribed Tenant Failed Setup Event"];

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