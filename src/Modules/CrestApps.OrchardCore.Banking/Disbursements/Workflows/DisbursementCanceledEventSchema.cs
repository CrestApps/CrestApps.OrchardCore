namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementCanceledEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementCanceledEvent);

    protected override string DisplayText => "Disbursement Canceled";

    protected override string Description => "Starts when a disbursement is cancelled, such as when its check is voided or stopped." + Input;
}
