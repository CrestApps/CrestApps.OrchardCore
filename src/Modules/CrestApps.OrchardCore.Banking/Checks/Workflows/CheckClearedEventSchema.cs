namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckClearedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckClearedEvent);

    protected override string DisplayText => "Check Cleared";

    protected override string Description => "Starts when the bank paid a check." + Input;
}
