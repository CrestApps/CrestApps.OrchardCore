namespace CrestApps.OrchardCore.Disbursements.Workflows;

internal sealed class DisbursementReturnedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementReturnedEvent);

    protected override string DisplayText => "Disbursement Returned";

    protected override string Description => "Starts when the bank returns a disbursement, such as for a closed account. FailureCode holds the return code." + Input;
}
