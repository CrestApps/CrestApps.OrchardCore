using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Describes a workflow activity that is registered in the activity library, along with the schema
/// contributed for it, when one exists.
/// </summary>
public sealed class WorkflowActivityDescriptor
{
    /// <summary>
    /// Gets the workflow activity name as registered in the activity library.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the activity is a workflow event.
    /// </summary>
    public required bool IsEvent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the activity is a workflow task.
    /// </summary>
    public required bool IsTask { get; init; }

    /// <summary>
    /// Gets the activity category.
    /// </summary>
    public string Category { get; init; }

    /// <summary>
    /// Gets the human readable activity title.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets the description explaining what the activity does.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the outcome names the activity can produce.
    /// </summary>
    public IReadOnlyList<string> Outcomes { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the activity produces outcomes derived from its own configuration.
    /// </summary>
    public bool HasDynamicOutcomes { get; init; }

    /// <summary>
    /// Gets a value indicating whether an <see cref="IWorkflowActivitySchemaDefinition"/> was found for the activity.
    /// </summary>
    public bool HasSchemaDefinition { get; init; }

    /// <summary>
    /// Gets the schema describing the activity's <c>Properties</c> object.
    /// </summary>
    public JsonSchemaBuilder Properties { get; init; }
}
