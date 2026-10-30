namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Names shared by the scheduling module and the modules that integrate with it.
/// </summary>
public static class SchedulingConstants
{
    /// <summary>
    /// The YesSql collection that holds resources, appointment types, appointments and waitlist entries.
    /// </summary>
    public const string CollectionName = "Scheduling";

    /// <summary>
    /// The source of the calendar events appointments create.
    /// </summary>
    public const string EventSource = "Scheduling";

    /// <summary>
    /// The calendar type of the calendars scheduling creates for resources.
    /// </summary>
    public const string CalendarType = "Appointments";

    /// <summary>
    /// The feature identifiers of the scheduling module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The base Scheduling feature.
        /// </summary>
        public const string Scheduling = "CrestApps.OrchardCore.Scheduling";

        /// <summary>
        /// Public booking pages and manage links.
        /// </summary>
        public const string SelfService = "CrestApps.OrchardCore.Scheduling.SelfService";

        /// <summary>
        /// The scheduling HTTP API.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Scheduling.Api";

        /// <summary>
        /// The scheduling AI tools.
        /// </summary>
        public const string AITools = "CrestApps.OrchardCore.Scheduling.AI";
    }
}
