namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchSettledEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchSettledEvent);

    protected override string DisplayText => "Disbursement Batch Settled";

    protected override string Description => "Starts when a batch is settled, by hand, by the Settle Disbursement Batches task, or at release when the provider paid everything; Settled counts the disbursements paid." + BatchInput;
}
