namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Abstraction that supplies the list of known feature IDs and theme IDs
/// to recipe step schema builders that need dynamic enum values.
/// </summary>
public interface IFeatureSchemaProvider
{
    /// <summary>Returns IDs of all available features.</summary>
    Task<IEnumerable<string>> GetFeatureIdsAsync();

    /// <summary>Returns IDs of themes only.</summary>
    Task<IEnumerable<string>> GetThemeIdsAsync();
}
