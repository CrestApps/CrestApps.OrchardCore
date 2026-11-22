namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckUnclearedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckUnclearedEvent);

    protected override string DisplayText => "Check Uncleared";

    protected override string Description => "Starts when a cleared check is recorded as not paid after all and goes back to the status it cleared from." + Input;
}
