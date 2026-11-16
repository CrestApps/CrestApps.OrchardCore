namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementFailedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementFailedEvent);

    protected override string DisplayText => "Disbursement Failed";

    protected override string Description => "Starts when a disbursement fails or is cancelled, such as when its check is voided." + Input;
}
