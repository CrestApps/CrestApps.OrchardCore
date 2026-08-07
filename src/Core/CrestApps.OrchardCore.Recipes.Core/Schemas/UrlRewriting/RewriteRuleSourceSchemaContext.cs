namespace CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;

/// <summary>
/// Provides contextual information about a rewrite rule source while its recipe schema is being built.
/// </summary>
public sealed class RewriteRuleSourceSchemaContext
{
    /// <summary>
    /// Gets the rewrite rule source name as reported by the schema definition.
    /// </summary>
    public required string SourceName { get; init; }
}
