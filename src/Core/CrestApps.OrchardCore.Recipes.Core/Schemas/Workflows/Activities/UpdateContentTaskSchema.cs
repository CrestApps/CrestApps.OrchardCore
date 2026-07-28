using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UpdateContentTask</c> workflow task.
/// </summary>
public sealed class UpdateContentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UpdateContentTask";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Update Content Task";

    /// <inheritdoc />
    protected override string Description => "Updates an existing content item by merging the provided properties into it";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Content"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the ContentItemId of the item to update. For example: correlationId(). The expression must return a string; the task throws if the result is empty or not a string."));
        yield return ("ContentItemIdExpression", WorkflowActivitySchemaBuilders.ScriptExpression("Deprecated. An earlier property for the content item ID expression. The display driver now stores the content item ID expression in Content instead; this property is persisted but never read during execution."));
        yield return ("ContentProperties", WorkflowActivitySchemaBuilders.LiquidExpression("An optional JSON object providing values for content parts, fields, and their properties to merge into the item. Defaults to {\"DisplayText\":\"Enter a title\"}."));
        yield return ("Publish", WorkflowActivitySchemaBuilders.Boolean("When true, the updated content item is published immediately. When false, a draft version is saved. Defaults to false."));
    }
}
