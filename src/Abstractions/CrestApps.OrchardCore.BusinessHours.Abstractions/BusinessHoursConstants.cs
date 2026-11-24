namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// Names shared by the Business Hours module and the modules that build on it.
/// </summary>
public static class BusinessHoursConstants
{
    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries business hours schedules.
    /// </summary>
    public const string RecipeStepName = "BusinessHoursSchedules";

    /// <summary>
    /// The feature identifiers of the Business Hours module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Business Hours feature: schedules kept and edited on their own, and the schedule picker.
        /// </summary>
        public const string BusinessHours = "CrestApps.OrchardCore.BusinessHours";
    }
}
