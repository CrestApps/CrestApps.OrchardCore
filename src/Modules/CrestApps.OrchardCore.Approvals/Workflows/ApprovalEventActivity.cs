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

    /// <summary>
    /// Gets or sets the approval type the event starts for, by name; empty for every type.
    /// </summary>
    public string ApprovalType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => string.IsNullOrWhiteSpace(ApprovalType)
            || (workflowContext.Input.TryGetValue("SubjectKind", out var kind) && string.Equals(kind as string, ApprovalType.Trim(), StringComparison.OrdinalIgnoreCase));

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
public sealed class ApprovalEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, ApprovalEventViewModel>
    where TActivity : ApprovalEventActivity
{
    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, ApprovalEventViewModel model)
        => model.ApprovalType = activity.ApprovalType;

    /// <inheritdoc />
    protected override void UpdateActivity(ApprovalEventViewModel model, TActivity activity)
        => activity.ApprovalType = string.IsNullOrWhiteSpace(model.ApprovalType) ? null : model.ApprovalType.Trim();
}

/// <summary>
/// The editor of an approval event: the type it starts for.
/// </summary>
public class ApprovalEventViewModel
{
    /// <summary>
    /// Gets or sets the approval type, or empty for every type.
    /// </summary>
    public string ApprovalType { get; set; }
}
