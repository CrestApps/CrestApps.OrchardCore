using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// A workflow event of the Disbursements module.
/// </summary>
public abstract class DisbursementEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected DisbursementEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Disbursements"];

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Displays a Disbursements workflow event in the workflow editor.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class DisbursementEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : DisbursementEventActivity
{
}
