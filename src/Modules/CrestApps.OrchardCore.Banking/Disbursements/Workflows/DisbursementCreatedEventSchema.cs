namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementCreatedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementCreatedEvent);

    protected override string DisplayText => "Disbursement Created";

    protected override string Description => "Starts when a disbursement is recorded." + Input;
}
