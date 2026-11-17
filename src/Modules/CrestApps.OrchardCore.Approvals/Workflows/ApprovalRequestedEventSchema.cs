namespace CrestApps.OrchardCore.Approvals.Workflows;

internal sealed class ApprovalRequestedEventSchema : ApprovalEventSchemaBase
{
    public override string Name => nameof(ApprovalRequestedEvent);

    protected override string DisplayText => "Approval Requested";

    protected override string Description => "Starts when someone asks for an approval. The workflow's correlation id is the request id." + Input;
}
