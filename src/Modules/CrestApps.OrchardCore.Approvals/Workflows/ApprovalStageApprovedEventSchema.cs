namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalStageApprovedEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalStageApprovedEvent);

    protected override string DisplayText => "Approval Stage Approved";

    protected override string Description => "Starts when enough approvers approve a stage that isn't the last, and the next stage starts. Stage is the new stage and ApproverUserNames its approvers. The workflow's correlation id is the request id." + Input;
}
