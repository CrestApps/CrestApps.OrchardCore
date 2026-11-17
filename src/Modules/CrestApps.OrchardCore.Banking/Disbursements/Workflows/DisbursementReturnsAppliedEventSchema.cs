namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

internal sealed class DisbursementReturnsAppliedEventSchema : DisbursementEventSchemaBase
{
    public override string Name => nameof(DisbursementReturnsAppliedEvent);

    protected override string DisplayText => "Disbursement Returns Applied";

    protected override string Description => "Starts when the bank's returns file is applied; each returned disbursement starts Disbursement Returned too." + ReturnsInput;
}
