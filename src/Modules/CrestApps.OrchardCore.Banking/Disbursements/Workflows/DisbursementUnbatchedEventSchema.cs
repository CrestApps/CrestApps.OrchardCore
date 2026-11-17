namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementUnbatchedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementUnbatchedEvent);

    protected override string DisplayText => "Disbursement Removed from Batch";

    protected override string Description => "Starts when a disbursement goes back to pending because its open batch was cancelled or reopened." + Input;
}
