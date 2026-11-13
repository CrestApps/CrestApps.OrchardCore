namespace CrestApps.OrchardCore.Checks.Workflows;

internal sealed class CheckPrintedEventSchema : CheckEventSchemaBase
{
    public override string Name => nameof(CheckPrintedEvent);

    protected override string DisplayText => "Check Printed";

    protected override string Description => "Starts when a check is printed." + Input;
}
