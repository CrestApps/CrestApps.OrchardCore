namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementFailedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementFailedEvent);

    protected override string DisplayText => "Disbursement Failed";

    protected override string Description => "Starts when a disbursement fails, such as when its check can't be issued. A cancelled or reversed disbursement starts Disbursement Canceled or Disbursement Reversed instead." + Input;
}
