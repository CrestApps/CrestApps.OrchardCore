using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Describes the recipe payload of a single workflow activity.
/// </summary>
public sealed class WorkflowActivitySchema
{
    /// <summary>
    /// Gets or sets the activity category, for example <c>Messaging</c> or <c>Content</c>.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the human readable activity title shown in the workflow editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the activity does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the outcome names the activity can produce. Transitions use these values in
    /// <c>Transitions[].SourceOutcomeName</c> to connect this activity to the next one.
    /// </summary>
    public IReadOnlyList<string> Outcomes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the activity can produce outcomes beyond those listed in
    /// <see cref="Outcomes"/>. Activities whose outcomes are computed from user supplied values, such as
    /// <c>ForkTask</c>, report <see langword="true"/> to indicate that <see cref="Outcomes"/> is not exhaustive.
    /// </summary>
    public bool HasDynamicOutcomes { get; set; }

    /// <summary>
    /// Gets or sets the schema describing the activity's <c>Properties</c> object in the recipe payload.
    /// </summary>
    public JsonSchemaBuilder Properties { get; set; }
}
