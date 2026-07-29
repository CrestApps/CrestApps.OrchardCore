using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ContentDraftSavedEvent</c> workflow event.
/// </summary>
public sealed class ContentDraftSavedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentDraftSavedEvent";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "Content Save Draft Event";

    /// <inheritdoc />
    protected override string Description => "Triggered when a draft version of a content item is saved";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Content", WorkflowActivitySchemaBuilders.ScriptExpression("A JavaScript expression that evaluates to the content item associated with this event. Typically omitted; the triggering content item is provided automatically by the workflow execution context."));
        yield return ("ContentTypeFilter", WorkflowActivitySchemaBuilders.StringArray("An optional list of content type names to filter on. Leave empty to match any content type. Defaults to []."));
    }
}
