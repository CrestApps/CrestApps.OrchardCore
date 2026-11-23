using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// The base of the mailing list member events. The workflow input holds <c>ListId</c>, <c>ListName</c>, <c>MemberId</c>,
/// <c>Email</c>, <c>Name</c>, <c>Status</c> and <c>Source</c>.
/// </summary>
public abstract class MailingListMemberEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected MailingListMemberEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category
        => S["Mailing Lists"];

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
