using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// Provides the shared behavior for Stripe workflow events, exposing a single "Done" outcome under the
/// "Stripe" category.
/// </summary>
public abstract class StripeEventActivity : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    protected StripeEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localized workflow activity category.
    /// </summary>
    public override LocalizedString Category
        => S["Stripe"];

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
    /// Resumes the workflow when the event is triggered.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="activityContext">The activity execution context.</param>
    /// <returns>The workflow activity execution result.</returns>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
