namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalApprovedEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalApprovedEvent);

    protected override string DisplayText => "Approval Approved";

    protected override string Description => "Starts when a request has the approvals it needs." + Input;
}
