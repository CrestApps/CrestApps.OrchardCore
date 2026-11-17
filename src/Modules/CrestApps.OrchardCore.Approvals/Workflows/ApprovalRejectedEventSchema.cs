namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalRejectedEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalRejectedEvent);

    protected override string DisplayText => "Approval Rejected";

    protected override string Description => "Starts when an approver rejects a request." + Input;
}
