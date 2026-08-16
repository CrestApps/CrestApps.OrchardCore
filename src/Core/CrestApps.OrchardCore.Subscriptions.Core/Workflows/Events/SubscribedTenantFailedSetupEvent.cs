using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;

/// <summary>
/// Represents a workflow event that resumes when subscribed tenant setup fails.
/// </summary>
public sealed class SubscribedTenantFailedSetupEvent : EventActivity
{
    /// <summary>
    /// The workflow event name used to trigger subscribed tenant setup failure workflows.
    /// </summary>
    public const string EventName = "SubscribedTenantFailedSetupEvent";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscribedTenantFailedSetupEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public SubscribedTenantFailedSetupEvent(IStringLocalizer<SubscribedTenantFailedSetupEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the workflow activity name.
    /// </summary>
    public override string Name
        => EventName;

    /// <summary>
    /// Gets the localized workflow activity display text.
    /// </summary>
    public override LocalizedString DisplayText
        => S["Subscribed Tenant Failed Setup Event"];

    /// <summary>
    /// Gets the localized workflow activity category.
    /// </summary>
    public override LocalizedString Category
        => S["Subscriptions"];

    /// <summary>
    /// Gets the possible outcomes produced by the workflow event.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="activityContext">The activity execution context.</param>
    /// <returns>The possible workflow outcomes.</returns>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome(S["Done"]);
    }

    /// <summary>
    /// Resumes the workflow when the subscribed tenant setup failure event is triggered.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="activityContext">The activity execution context.</param>
    /// <returns>The workflow activity execution result.</returns>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
