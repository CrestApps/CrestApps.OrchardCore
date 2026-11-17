namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementSubmittedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementSubmittedEvent);

    protected override string DisplayText => "Disbursement Submitted";

    protected override string Description => "Starts when a disbursement is sent: written to a bank file, paid by an issued check, or handed to a provider." + Input;
}
