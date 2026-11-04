namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Names shared by the Teams module and the modules that build on it.
/// </summary>
public static class TeamsConstants
{
    /// <summary>
    /// The YesSql collection that holds teams.
    /// </summary>
    public const string CollectionName = "Teams";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries teams.
    /// </summary>
    public const string RecipeStepName = "Teams";

    /// <summary>
    /// The feature identifiers of the Teams module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Teams feature.
        /// </summary>
        public const string Teams = "CrestApps.OrchardCore.Teams";
    }
}
