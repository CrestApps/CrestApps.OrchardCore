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
    /// The name of the recipe step, and of the deployment step, that carries the scheduling setup.
    /// </summary>
    public const string RecipeStepName = "Scheduling";

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

    }

    /// <summary>
    /// The rate-limit groups of the scheduling endpoints. A Rate Limits policy can target them.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public booking page's booking post.
        /// </summary>
        public const string Booking = "scheduling-booking";
    }

    /// <summary>
    /// The attempt policies of the scheduling endpoints, overridable in the <c>CrestApps_Throttling:AttemptPolicies</c>
    /// configuration section.
    /// </summary>
    public static class AttemptPolicies
    {
        /// <summary>
        /// Bookings from the public booking page, per network.
        /// </summary>
        public const string BookingNetwork = "scheduling-booking-network";
    }
}
