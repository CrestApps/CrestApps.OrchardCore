using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>PublishContentTask</c> workflow task.
/// </summary>
public sealed class PublishContentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "PublishContentTask";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Publish Content Task";

    /// <inheritdoc />
    protected override string Description => "Publishes a content item resolved from the provided expression or the current workflow content item context";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Published", "Noop"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the content item (or its ContentItemId) to publish. For example: input('ContentItem'). If omitted, the workflow's ContentItem input variable or property is used."));
    }
}
