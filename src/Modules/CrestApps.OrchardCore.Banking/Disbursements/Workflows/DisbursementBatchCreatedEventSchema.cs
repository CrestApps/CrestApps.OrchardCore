namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchCreatedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchCreatedEvent);

    protected override string DisplayText => "Disbursement Batch Created";

    protected override string Description => "Starts when a batch is made from pending disbursements." + BatchInput;
}
