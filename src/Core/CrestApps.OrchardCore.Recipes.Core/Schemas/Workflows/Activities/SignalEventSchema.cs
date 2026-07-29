using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>SignalEvent</c> workflow event.
/// </summary>
public sealed class SignalEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SignalEvent";

    /// <inheritdoc />
    protected override string Category => "HTTP";

    /// <inheritdoc />
    protected override string DisplayText => "Signal Event";

    /// <inheritdoc />
    protected override string Description => "Triggers a workflow when a matching signal is received";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("SignalName", WorkflowActivitySchemaBuilders.LiquidExpression("The name of the signal that triggers this event."));
    }
}
