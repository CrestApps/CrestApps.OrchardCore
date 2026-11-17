namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementReversedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementReversedEvent);

    protected override string DisplayText => "Disbursement Reversed";

    protected override string Description => "Starts when a settled disbursement is taken back." + Input;
}
