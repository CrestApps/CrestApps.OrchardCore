namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchReopenedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchReopenedEvent);

    protected override string DisplayText => "Disbursement Batch Reopened";

    protected override string Description => "Starts when a batch stuck in sending is reopened to be sent again; Reason says why." + BatchInput;
}
