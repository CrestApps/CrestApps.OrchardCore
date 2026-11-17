namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementBatchCanceledEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementBatchCanceledEvent);

    protected override string DisplayText => "Disbursement Batch Canceled";

    protected override string Description => "Starts when an open batch is cancelled and its disbursements go back to pending." + BatchInput;
}
