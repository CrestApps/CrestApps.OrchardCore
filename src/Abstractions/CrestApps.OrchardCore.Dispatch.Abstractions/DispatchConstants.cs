namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// The constants of the Dispatch module.
/// </summary>
public static class DispatchConstants
{
    /// <summary>
    /// The YesSql collection visits are kept in.
    /// </summary>
    public const string CollectionName = "Dispatch";

    /// <summary>
    /// The source of the calendar events Dispatch keeps for each assigned resource.
    /// </summary>
    public const string Source = "Dispatch";

    /// <summary>
    /// The first characters of a visit number, such as V-000001.
    /// </summary>
    public const string VisitNumberPrefix = "V-";

    /// <summary>
    /// The number sequence series that visit numbers come from.
    /// </summary>
    public const string VisitNumberSeries = "Dispatch.Visit";

    /// <summary>
    /// The calendar event type of a visit on a worker's team calendar.
    /// </summary>
    public const string JobEventType = "Job";

    /// <summary>
    /// The site settings group of the dispatch settings.
    /// </summary>
    public const string SettingsGroupId = "dispatch";

    /// <summary>
    /// The first part of the name of the lock taken on a resource while it is assigned.
    /// </summary>
    public const string ResourceLockPrefix = "DISPATCH_RESOURCE_";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Dispatch: visits, candidates, assignment and the board.
        /// </summary>
        public const string Dispatch = "CrestApps.OrchardCore.Dispatch";

        /// <summary>
        /// Technician Mobile: the field app in My Work, offline, with location sharing.
        /// </summary>
        public const string Mobile = "CrestApps.OrchardCore.Dispatch.Mobile";

        /// <summary>
        /// Field Service API: work orders, visits, optimizing and geo over HTTP.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Dispatch.Api";
    }

    /// <summary>
    /// The configuration section of the technician app's settings.
    /// </summary>
    public const string MobileConfigurationSection = "CrestApps:Dispatch:Mobile";

    /// <summary>
    /// The kinds of resources a visit is assigned to.
    /// </summary>
    public static class ResourceKinds
    {
        /// <summary>
        /// A worker.
        /// </summary>
        public const string Worker = "Worker";

        /// <summary>
        /// A piece of equipment, such as a lift.
        /// </summary>
        public const string Equipment = "Equipment";
    }

    /// <summary>
    /// The kinds of customer notices.
    /// </summary>
    public static class Notices
    {
        /// <summary>
        /// The visit was scheduled or moved.
        /// </summary>
        public const string Scheduled = "Scheduled";

        /// <summary>
        /// The day before the visit.
        /// </summary>
        public const string Reminder = "Reminder";

        /// <summary>
        /// The technician is on the way.
        /// </summary>
        public const string EnRoute = "EnRoute";

        /// <summary>
        /// The visit is done.
        /// </summary>
        public const string Completed = "Completed";

        /// <summary>
        /// The visit was canceled.
        /// </summary>
        public const string Canceled = "Canceled";
    }

    /// <summary>
    /// The names of the visit events observers hear about.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// A visit was created.
        /// </summary>
        public const string Created = "VisitCreated";

        /// <summary>
        /// A visit was assigned, reassigned or moved.
        /// </summary>
        public const string Assigned = "VisitAssigned";

        /// <summary>
        /// A visit was taken off its workers.
        /// </summary>
        public const string Unassigned = "VisitUnassigned";

        /// <summary>
        /// A visit's status changed.
        /// </summary>
        public const string StatusChanged = "VisitStatusChanged";

        /// <summary>
        /// A visit is at risk: running over, late to start, or its technician is off.
        /// </summary>
        public const string AtRisk = "VisitAtRisk";
    }
}
