namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;

/// <summary>
/// Provides contextual information about a query source while its recipe schema is being built.
/// </summary>
public sealed class QuerySourceSchemaContext
{
    /// <summary>
    /// Gets the query source name as reported by the schema definition.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the example values from the current tenant that sources surface as non-restrictive suggestions.
    /// </summary>
    public RecipeSchemaExamples Examples { get; init; } = RecipeSchemaExamples.Empty;
}
