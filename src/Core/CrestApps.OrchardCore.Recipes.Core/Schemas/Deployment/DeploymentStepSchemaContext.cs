namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;

/// <summary>
/// Provides contextual information about a deployment step while its recipe schema is being built.
/// </summary>
public sealed class DeploymentStepSchemaContext
{
    /// <summary>
    /// Gets the deployment step type name as reported by the deployment step factory.
    /// </summary>
    public required string StepType { get; init; }

    /// <summary>
    /// Gets the example values from the current tenant that steps surface as non-restrictive suggestions.
    /// </summary>
    public RecipeSchemaExamples Examples { get; init; } = RecipeSchemaExamples.Empty;
}
