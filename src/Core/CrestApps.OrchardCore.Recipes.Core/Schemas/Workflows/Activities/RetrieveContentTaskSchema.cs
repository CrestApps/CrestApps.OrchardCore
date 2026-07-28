using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>RetrieveContentTask</c> workflow task.
/// </summary>
public sealed class RetrieveContentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "RetrieveContentTask";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Retrieve Content Task";

    /// <inheritdoc />
    protected override string Description => "Retrieves a content item by ID and stores it in the workflow execution context";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Retrieved"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Content"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the ContentItemId of the item to retrieve. For example: correlationId(). The expression must return a string; the task throws if the result is empty or not a string."));
    }
}
