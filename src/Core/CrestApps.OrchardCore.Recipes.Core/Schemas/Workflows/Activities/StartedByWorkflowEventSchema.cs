using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>StartedByWorkflowEvent</c> workflow event.
/// </summary>
public sealed class StartedByWorkflowEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StartedByWorkflowEvent";

    /// <inheritdoc />
    protected override string Category => "Primitives";

    /// <inheritdoc />
    protected override string DisplayText => "Started By Workflow Event";

    /// <inheritdoc />
    protected override string Description => "The start activity of a workflow type that other workflows run with the 'ExecuteWorkflowTask'. Set 'IsActivity' to true on the workflow type; its input variables are set from the task's inputs";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
