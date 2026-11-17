namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckIssuedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckIssuedEvent);

    protected override string DisplayText => "Check Issued";

    protected override string Description => "Starts when a check is issued with its number." + Input;
}
