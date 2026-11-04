namespace CrestApps.OrchardCore.Workforce.ContactCenter;

/// <summary>
/// The constants of contact center workforce management.
/// </summary>
public static class WorkforceContactCenterConstants
{
    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Workforce for Contact Center: a worker for every agent.
        /// </summary>
        public const string ContactCenter = "CrestApps.OrchardCore.Workforce.ContactCenter";

        /// <summary>
        /// Clocks agents in and out when they sign in and out of the contact center.
        /// </summary>
        public const string SignInPunches = "CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches";

        /// <summary>
        /// Real-time and historical adherence.
        /// </summary>
        public const string Adherence = "CrestApps.OrchardCore.Workforce.Adherence";

        /// <summary>
        /// Forecasting and Erlang staffing.
        /// </summary>
        public const string Forecasting = "CrestApps.OrchardCore.Workforce.Forecasting";

        /// <summary>
        /// Intraday management: the day as it happens, reforecasts and voluntary overtime and time off.
        /// </summary>
        public const string Intraday = "CrestApps.OrchardCore.Workforce.Intraday";

        /// <summary>
        /// Agents' states kept in Time Tracker, each in its stage.
        /// </summary>
        public const string TimeTracker = "CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker";
    }

    /// <summary>
    /// The id agents' state entries are kept under in Time Tracker.
    /// </summary>
    public const string TimeTrackerClockId = "Workforce.ContactCenter";
}
