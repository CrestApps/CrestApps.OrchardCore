namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;

/// <summary>
/// Provides contextual information about a workflow activity while its recipe schema is being built.
/// </summary>
public sealed class WorkflowActivitySchemaContext
{
    /// <summary>
    /// Gets the workflow activity name as registered in the activity library.
    /// </summary>
    public required string ActivityName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the activity is a workflow event, meaning it can start a workflow.
    /// </summary>
    public required bool IsEvent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the activity is a workflow task.
    /// </summary>
    public required bool IsTask { get; init; }

    /// <summary>
    /// Gets the localized category reported by the activity, when the activity library resolved one.
    /// </summary>
    public string Category { get; init; }

    /// <summary>
    /// Gets the localized display text reported by the activity, when the activity library resolved one.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets the example values from the current tenant that activities surface as non-restrictive suggestions.
    /// </summary>
    public RecipeSchemaExamples Examples { get; init; } = RecipeSchemaExamples.Empty;
}
