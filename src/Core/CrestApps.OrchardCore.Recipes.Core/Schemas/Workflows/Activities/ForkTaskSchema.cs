using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ForkTask</c> workflow task.
/// </summary>
public sealed class ForkTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ForkTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "Fork Task";

    /// <inheritdoc />
    protected override string Description => "Splits workflow execution into multiple concurrent branches, taking every outcome listed in 'Forks' at the same time";

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Forks"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Forks", WorkflowActivitySchemaBuilders.StringArray("The fork names. Each entry becomes an outcome of this activity and can be used as a 'Transitions[].SourceOutcomeName'."));
    }
}
