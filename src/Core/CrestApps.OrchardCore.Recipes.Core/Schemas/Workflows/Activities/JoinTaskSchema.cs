using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>JoinTask</c> workflow task.
/// </summary>
public sealed class JoinTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "JoinTask";

    /// <inheritdoc />
    protected override string Category => "Control Flow";

    /// <inheritdoc />
    protected override string DisplayText => "Join Task";

    /// <inheritdoc />
    protected override string Description => "Merges concurrent branches created by a fork back into a single execution path";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Joined"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Mode", WorkflowActivitySchemaBuilders.EnumValue("Controls whether the join waits for all inbound branches or for any single inbound branch to complete. Defaults to 'WaitAll'.", "WaitAll", "WaitAny"));
        yield return ("Branches", WorkflowActivitySchemaBuilders.StringArray("Runtime state. Tracks the inbound transitions that already reached this activity and is normally omitted from recipes."));
    }
}
