namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Provides a snapshot of well-known values from the current tenant that recipe schema definitions surface as
/// non-restrictive JSON Schema <c>examples</c>. The values make generated schemas reflect the live tenant while
/// still allowing any custom value.
/// </summary>
public sealed class RecipeSchemaExamples
{
    /// <summary>
    /// Gets a shared empty snapshot used when no example source is available, such as during static export.
    /// </summary>
    public static readonly RecipeSchemaExamples Empty = new();

    /// <summary>
    /// Gets the technical names of the content types defined on the current tenant.
    /// </summary>
    public IReadOnlyList<string> ContentTypeNames { get; init; } = [];

    /// <summary>
    /// Gets the technical names of the content parts defined on the current tenant.
    /// </summary>
    public IReadOnlyList<string> ContentPartNames { get; init; } = [];

    /// <summary>
    /// Gets the cultures supported by the current tenant.
    /// </summary>
    public IReadOnlyList<string> CultureNames { get; init; } = [];

    /// <summary>
    /// Gets the names of the roles defined on the current tenant.
    /// </summary>
    public IReadOnlyList<string> RoleNames { get; init; } = [];

    /// <summary>
    /// Gets the names of the index profiles defined on the current tenant.
    /// </summary>
    public IReadOnlyList<string> IndexProfileNames { get; init; } = [];
}
