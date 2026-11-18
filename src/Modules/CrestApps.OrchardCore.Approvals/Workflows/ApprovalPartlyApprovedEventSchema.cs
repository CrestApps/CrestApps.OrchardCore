namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalPartlyApprovedEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalPartlyApprovedEvent);

    protected override string DisplayText => "Approval Partly Approved";

    protected override string Description => "Starts when an approver approves and more approvals are needed in the same stage. The workflow's correlation id is the request id." + Input;
}
