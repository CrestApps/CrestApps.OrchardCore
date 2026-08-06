namespace CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;

/// <summary>
/// Provides contextual information about an admin menu node while its recipe schema is being built.
/// </summary>
public sealed class AdminNodeSchemaContext
{
    /// <summary>
    /// Gets the admin menu node name as reported by the schema definition.
    /// </summary>
    public required string NodeName { get; init; }
}
