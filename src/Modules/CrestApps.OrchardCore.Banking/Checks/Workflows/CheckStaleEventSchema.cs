namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckStaleEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckStaleEvent);

    protected override string DisplayText => "Check Stale";

    protected override string Description => "Starts when an outstanding check becomes too old to pay." + Input;
}
