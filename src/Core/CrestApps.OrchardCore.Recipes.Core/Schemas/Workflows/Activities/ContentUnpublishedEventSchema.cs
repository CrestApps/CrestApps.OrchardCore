using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ContentUnpublishedEvent</c> workflow event.
/// </summary>
public sealed class ContentUnpublishedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentUnpublishedEvent";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Content Unpublished Event";

    /// <inheritdoc />
    protected override string Description => "Triggered when a content item is unpublished";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the content item associated with this event. Typically omitted; the triggering content item is provided automatically by the workflow execution context."));
        yield return ("ContentTypeFilter", WorkflowActivitySchemaBuilders.StringArray("An optional list of content type names to filter on. Leave empty to match any content type. Defaults to []."));
    }
}
