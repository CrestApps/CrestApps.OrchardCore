namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementSettledEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementSettledEvent);

    protected override string DisplayText => "Disbursement Settled";

    protected override string Description => "Starts when a disbursement is paid: its batch settled, its check cleared, or it was paid outside the system." + Input;
}
