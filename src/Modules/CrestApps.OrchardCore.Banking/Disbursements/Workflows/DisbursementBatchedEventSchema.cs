namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchedEvent);

    protected override string DisplayText => "Disbursement Added to Batch";

    protected override string Description => "Starts when a disbursement is put in a batch; BatchId is the batch." + Input;
}
