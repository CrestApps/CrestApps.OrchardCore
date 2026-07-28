using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ScriptTask</c> workflow task.
/// </summary>
public sealed class ScriptTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ScriptTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "Script Task";

    /// <inheritdoc />
    protected override string Description => "Executes a JavaScript block that selects its own outcomes by calling setOutcome";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("AvailableOutcomes", WorkflowActivitySchemaBuilders.StringArray("The outcomes the script may select. Each entry becomes an outcome of this activity. Defaults to a single \"Done\" outcome."));
        yield return ("Script", WorkflowActivitySchemaBuilders.ScriptExpression("The script to execute. Call setOutcome with one of the values listed in 'AvailableOutcomes' at least once. Defaults to \"setOutcome('Done');\"."));
    }
}
