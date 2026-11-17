using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts when enough approvers approve a stage that isn't the last, and the next stage starts. Stage is the new stage and ApproverUserNames its approvers. The workflow's correlation id is the request id.
/// </summary>
public sealed class ApprovalStageApprovedEvent : ApprovalEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalStageApprovedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalStageApprovedEvent(IStringLocalizer<ApprovalStageApprovedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(ApprovalStageApprovedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Approval Stage Approved"];
}
