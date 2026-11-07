namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Constants of the CrestApps Recipes module.
/// </summary>
public static class CrestAppsRecipesConstants
{
    /// <summary>
    /// The feature identifiers of the Recipes module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The feature that describes recipe steps with JSON schemas. A module registers the schemas of its own recipe
        /// steps and settings in a startup that requires this feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Recipes";
    }
}
