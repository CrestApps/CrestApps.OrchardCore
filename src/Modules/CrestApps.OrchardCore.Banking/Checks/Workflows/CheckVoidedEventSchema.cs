namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckVoidedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckVoidedEvent);

    protected override string DisplayText => "Check Voided";

    protected override string Description => "Starts when a check is voided, stopped or replaced by a reissued check (Change says which)." + Input;
}
