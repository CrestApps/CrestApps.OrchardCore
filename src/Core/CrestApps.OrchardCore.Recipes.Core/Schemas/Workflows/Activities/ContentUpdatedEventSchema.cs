using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ContentUpdatedEvent</c> workflow event.
/// </summary>
public sealed class ContentUpdatedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentUpdatedEvent";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Content Updated Event";

    /// <inheritdoc />
    protected override string Description => "Triggered when a content item is updated";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the content item associated with this event. Typically omitted; the triggering content item is provided automatically by the workflow execution context."));
        yield return ("ContentTypeFilter", WorkflowActivitySchemaBuilders.StringArray("An optional list of content type names to filter on. Leave empty to match any content type. Defaults to [].", context.Examples.ContentTypeNames));
    }
}
