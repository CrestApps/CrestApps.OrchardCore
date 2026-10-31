namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Names shared by the Skills module and the modules that integrate with it.
/// </summary>
public static class SkillsConstants
{
    /// <summary>
    /// The YesSql collection that holds skills, assignments, certifications and certification records.
    /// </summary>
    public const string CollectionName = "Skills";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries skills and certifications.
    /// </summary>
    public const string RecipeStepName = "Skills";

    /// <summary>
    /// The feature identifiers of the Skills module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Skills feature.
        /// </summary>
        public const string Skills = "CrestApps.OrchardCore.Skills";
    }
}
