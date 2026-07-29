using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CreateContentTask</c> workflow task.
/// </summary>
public sealed class CreateContentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CreateContentTask";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Create Content Task";

    /// <inheritdoc />
    protected override string Description => "Creates a new content item of a specified content type";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ContentType"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("Inherited from the activity base class but not used by this task; content is always created from scratch using ContentType."));
        yield return ("ContentType", WorkflowActivitySchemaBuilders.String("The content type name of the item to create."));
        yield return ("Publish", WorkflowActivitySchemaBuilders.Boolean("When true, the created content item is published immediately. When false, a draft version is saved. Defaults to false."));
        yield return ("ContentProperties", WorkflowActivitySchemaBuilders.LiquidExpression("An optional JSON object providing values for content parts, fields, and their properties to apply when creating the item. Defaults to {\"DisplayText\":\"Enter a title\"}."));
    }
}
