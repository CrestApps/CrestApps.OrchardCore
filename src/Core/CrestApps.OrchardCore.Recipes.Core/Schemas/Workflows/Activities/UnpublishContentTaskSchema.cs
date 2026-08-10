using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UnpublishContentTask</c> workflow task.
/// </summary>
public sealed class UnpublishContentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UnpublishContentTask";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Unpublish Content Task";

    /// <inheritdoc />
    protected override string Description => "Unpublishes a content item resolved from the provided expression or the current workflow content item context";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Unpublished", "Noop"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the content item (or its ContentItemId) to unpublish. For example: input('ContentItem'). If omitted, the workflow's ContentItem input variable or property is used."));
    }
}
