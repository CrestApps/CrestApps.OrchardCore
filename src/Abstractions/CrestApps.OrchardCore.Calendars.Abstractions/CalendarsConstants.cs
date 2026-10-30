namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Names shared by the calendar modules and the modules that integrate with them.
/// </summary>
public static class CalendarsConstants
{
    /// <summary>
    /// The YesSql collection that holds calendars, events and reminders.
    /// </summary>
    public const string CollectionName = "Calendars";

    /// <summary>
    /// The name of the recipe and deployment step that imports and exports calendars with their events.
    /// </summary>
    public const string RecipeStepName = "Calendars";

    /// <summary>
    /// The feature identifiers of the calendar modules.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The base Calendars feature: calendars, events, the calendar view and the General and Personal types.
        /// </summary>
        public const string Calendars = "CrestApps.OrchardCore.Calendars";

        /// <summary>
        /// Team calendars owned by a role, with the team availability view.
        /// </summary>
        public const string Teams = "CrestApps.OrchardCore.Calendars.Teams";

        /// <summary>
        /// Room and equipment calendars that can be invited to events.
        /// </summary>
        public const string Resources = "CrestApps.OrchardCore.Calendars.Resources";

        /// <summary>
        /// Workforce calendars with shifts, time off and training.
        /// </summary>
        public const string Workforce = "CrestApps.OrchardCore.Calendars.Workforce";

        /// <summary>
        /// Office calendars with desk capacity.
        /// </summary>
        public const string Office = "CrestApps.OrchardCore.Calendars.Office";

        /// <summary>
        /// Invitation, update and cancellation emails for attendees.
        /// </summary>
        public const string Invitations = "CrestApps.OrchardCore.Calendars.Invitations";

        /// <summary>
        /// Durable starting-soon and ended processing for events.
        /// </summary>
        public const string Reminders = "CrestApps.OrchardCore.Calendars.Reminders";

        /// <summary>
        /// Private iCalendar subscription feeds and .ics import and export.
        /// </summary>
        public const string Feeds = "CrestApps.OrchardCore.Calendars.Feeds";

        /// <summary>
        /// Connections to external calendars and their synchronization.
        /// </summary>
        public const string External = "CrestApps.OrchardCore.Calendars.External";

        /// <summary>
        /// The calendars HTTP API.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Calendars.Api";

        /// <summary>
        /// The calendar AI tools.
        /// </summary>
        public const string AITools = "CrestApps.OrchardCore.Calendars.AI";
    }

    /// <summary>
    /// The names of the calendar types the calendar modules register.
    /// </summary>
    public static class Types
    {
        /// <summary>
        /// A shared calendar that belongs to nobody in particular, such as a company calendar.
        /// </summary>
        public const string General = "General";

        /// <summary>
        /// A calendar that belongs to one user.
        /// </summary>
        public const string Personal = "Personal";

        /// <summary>
        /// A calendar that belongs to a team, which is a role.
        /// </summary>
        public const string Team = "Team";

        /// <summary>
        /// A meeting room or another space that only one group can use at a time.
        /// </summary>
        public const string Room = "Room";

        /// <summary>
        /// A piece of equipment that can be reserved.
        /// </summary>
        public const string Equipment = "Equipment";

        /// <summary>
        /// Shifts, time off and training for a group of employees.
        /// </summary>
        public const string Workforce = "Workforce";

        /// <summary>
        /// Who is in an office and which desks are booked.
        /// </summary>
        public const string Office = "Office";
    }

    /// <summary>
    /// The names of the event types the calendar modules register.
    /// </summary>
    public static class EventTypes
    {
        /// <summary>
        /// The event type every calendar type has, used when a type names no event types or an event names none.
        /// </summary>
        public const string Default = "Event";
    }

    /// <summary>
    /// The value of <c>CalendarEvent.ModifiedBy</c> and of the change context when a workflow made the change.
    /// </summary>
    public const string WorkflowActor = "workflow";
}
