using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Provides a snapshot of well-known values from the current tenant that recipe schema definitions surface as
/// non-restrictive JSON Schema <c>examples</c>.
/// </summary>
public interface IRecipeSchemaExampleService
{
    /// <summary>
    /// Gets the example values available on the current tenant.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<RecipeSchemaExamples> GetExamplesAsync(CancellationToken cancellationToken = default);
}
