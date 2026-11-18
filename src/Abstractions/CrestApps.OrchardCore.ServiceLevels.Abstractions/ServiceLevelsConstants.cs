namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// Constants of the Service Levels building block.
/// </summary>
public static class ServiceLevelsConstants
{
    /// <summary>
    /// The YesSql collection that holds policies and timers.
    /// </summary>
    public const string CollectionName = "ServiceLevels";

    /// <summary>
    /// The name of the recipe step that imports policies.
    /// </summary>
    public const string RecipeStepName = "ServiceLevelPolicies";

    /// <summary>
    /// The warning point used when a policy doesn't set one: a timer warns once this share of its target has passed.
    /// </summary>
    public const int DefaultWarningPercent = 80;

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Service Levels feature.
        /// </summary>
        public const string ServiceLevels = "CrestApps.OrchardCore.ServiceLevels";
    }
}
