namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalCanceledEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalCanceledEvent);

    protected override string DisplayText => "Approval Canceled";

    protected override string Description => "Starts when an approval request is withdrawn or cancelled; Note is the reason. The workflow's correlation id is the request id." + Input;
}
