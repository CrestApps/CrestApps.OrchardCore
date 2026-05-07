namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Abstraction that supplies the list of known feature IDs and theme IDs
/// to recipe step schema builders that need dynamic enum values.
/// </summary>
public interface IFeatureSchemaProvider
{
    /// <summary>Returns IDs of all available features.</summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<string>> GetFeatureIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns IDs of themes only.</summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<string>> GetThemeIdsAsync(CancellationToken cancellationToken = default);
}
