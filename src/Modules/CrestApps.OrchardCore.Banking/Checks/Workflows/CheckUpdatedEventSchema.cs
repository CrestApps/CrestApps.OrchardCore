namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckUpdatedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckUpdatedEvent);

    protected override string DisplayText => "Check Edited";

    protected override string Description => "Starts when a check waiting to print is edited: its payee, address, amount, date or memo changed." + Input;
}
