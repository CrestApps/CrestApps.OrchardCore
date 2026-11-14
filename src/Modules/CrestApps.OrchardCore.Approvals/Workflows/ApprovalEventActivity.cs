using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// A workflow event of the Approvals module.
/// </summary>
public abstract class ApprovalEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ApprovalEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Approvals"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Displays an Approvals workflow event in the workflow editor.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class ApprovalEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : ApprovalEventActivity
{
}
