namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckVoidedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckVoidedEvent);

    protected override string DisplayText => "Check Voided";

    protected override string Description => "Starts when a check is voided (VoidedOn is the void date), or when a misprinted sheet of a print run is kept as a spoiled check (Change is Spoiled). A stopped or reissued check starts Check Stop Payment or Check Reissued instead." + Input;
}
