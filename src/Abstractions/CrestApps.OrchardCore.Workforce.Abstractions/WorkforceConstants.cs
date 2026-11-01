namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Names shared by the Workforce modules and the modules that integrate with them.
/// </summary>
public static class WorkforceConstants
{
    /// <summary>
    /// The YesSql collection that holds workers, teams, activity codes and the scheduling records.
    /// </summary>
    public const string CollectionName = "Workforce";

    /// <summary>
    /// The calendar type of a team's Workforce calendar.
    /// </summary>
    public const string CalendarType = "Workforce";

    /// <summary>
    /// The source of the calendars and events Workforce owns.
    /// </summary>
    public const string Source = "Workforce";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries the workforce setup.
    /// </summary>
    public const string RecipeStepName = "Workforce";

    /// <summary>
    /// The name of the HTTP client web push notifications are sent with.
    /// </summary>
    public const string WebPushHttpClient = "WorkforceWebPush";

    /// <summary>
    /// The name of the built-in shift schedule solver.
    /// </summary>
    public const string DefaultSolver = "Default";

    /// <summary>
    /// The event types of the Workforce calendar.
    /// </summary>
    public static class EventTypes
    {
        /// <summary>
        /// A published shift. Scheduling reads events of this name for resources whose hours come from shifts.
        /// </summary>
        public const string Shift = "Shift";

        /// <summary>
        /// Approved time off.
        /// </summary>
        public const string TimeOff = "TimeOff";

        /// <summary>
        /// Training.
        /// </summary>
        public const string Training = "Training";

        /// <summary>
        /// A team meeting.
        /// </summary>
        public const string Meeting = "Meeting";

        /// <summary>
        /// An on-call period, which leaves the worker free unless they are called.
        /// </summary>
        public const string OnCall = "OnCall";
    }

    /// <summary>
    /// The feature identifiers of the Workforce modules.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The base Workforce feature: workers, teams, activity codes and the Workforce calendar.
        /// </summary>
        public const string Workforce = "CrestApps.OrchardCore.Workforce";

        /// <summary>
        /// Shift planning: templates, patterns, coverage, schedules and publishing.
        /// </summary>
        public const string ShiftPlanning = "CrestApps.OrchardCore.Workforce.ShiftPlanning";

        /// <summary>
        /// Time off: types, policies, balances, requests and approvals.
        /// </summary>
        public const string TimeOff = "CrestApps.OrchardCore.Workforce.TimeOff";

        /// <summary>
        /// Labor rules: the rule catalog, rule sets and predictability pay.
        /// </summary>
        public const string LaborRules = "CrestApps.OrchardCore.Workforce.LaborRules";

        /// <summary>
        /// Time and attendance: punches, timesheets and payroll export.
        /// </summary>
        public const string TimeClock = "CrestApps.OrchardCore.Workforce.TimeClock";

        /// <summary>
        /// The My Work self-service portal.
        /// </summary>
        public const string Portal = "CrestApps.OrchardCore.Workforce.Portal";

        /// <summary>
        /// The workforce HTTP API.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Workforce.Api";
    }
}
