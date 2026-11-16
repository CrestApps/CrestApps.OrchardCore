namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchReleasedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchReleasedEvent);

    protected override string DisplayText => "Disbursement Batch Released";

    protected override string Description => "Starts when a batch is released to its provider: its bank file is written or its checks issued. Settled counts what the provider paid at once." + BatchInput;
}
