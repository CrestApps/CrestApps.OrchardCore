namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

internal sealed class CheckReissuedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckReissuedEvent);

    protected override string DisplayText => "Check Reissued";

    protected override string Description => "Starts when a check is replaced by a new one; ReplacedById is the new check. Check Voided starts too." + Input;
}
