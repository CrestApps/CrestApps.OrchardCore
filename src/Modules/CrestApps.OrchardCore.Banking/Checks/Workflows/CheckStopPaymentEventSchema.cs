namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckStopPaymentEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckStopPaymentEvent);

    protected override string DisplayText => "Check Stop Payment";

    protected override string Description => "Starts when the bank is asked not to pay a check. Check Voided starts too." + Input;
}
