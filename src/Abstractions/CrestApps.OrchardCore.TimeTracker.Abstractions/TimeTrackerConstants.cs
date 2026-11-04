namespace CrestApps.OrchardCore.TimeTracker;

/// <summary>
/// The constants of the Time Tracker module.
/// </summary>
public static class TimeTrackerConstants
{
    /// <summary>
    /// The YesSql collection time is kept in.
    /// </summary>
    public const string CollectionName = "TimeTracker";

    /// <summary>
    /// The name of the recipe step and deployment step.
    /// </summary>
    public const string RecipeStepName = "TimeTracker";

    /// <summary>
    /// The first part of the name of the lock taken on a person while their time changes.
    /// </summary>
    public const string UserLockPrefix = "TIMETRACKER_USER_";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Time Tracker.
        /// </summary>
        public const string TimeTracker = "CrestApps.OrchardCore.TimeTracker";
    }

    /// <summary>
    /// Where time entries come from.
    /// </summary>
    public static class Sources
    {
        /// <summary>
        /// The person, on their clock.
        /// </summary>
        public const string Self = "Self";

        /// <summary>
        /// A manager.
        /// </summary>
        public const string Manager = "Manager";

        /// <summary>
        /// A kiosk.
        /// </summary>
        public const string Kiosk = "Kiosk";

        /// <summary>
        /// A phone or tablet app.
        /// </summary>
        public const string Mobile = "Mobile";

        /// <summary>
        /// The Contact Center, from the agent's state.
        /// </summary>
        public const string ContactCenter = "ContactCenter";

        /// <summary>
        /// Approved time off.
        /// </summary>
        public const string TimeOff = "TimeOff";

        /// <summary>
        /// The API.
        /// </summary>
        public const string Api = "Api";

        /// <summary>
        /// A recipe or deployment import.
        /// </summary>
        public const string Import = "Import";
    }
}
