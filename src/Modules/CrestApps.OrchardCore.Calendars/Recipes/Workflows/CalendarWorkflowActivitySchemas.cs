using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Calendars.Recipes.Workflows;

/// <summary>
/// Describes the filters every calendar workflow event shares.
/// </summary>
internal abstract class CalendarEventActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Calendar";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CalendarIds", WorkflowActivitySchemaBuilders.StringArray("The identifiers of the calendars whose events start the workflow. Empty for every calendar."));
        yield return ("CalendarTypes", WorkflowActivitySchemaBuilders.StringArray("The technical names of the calendar types whose events start the workflow, such as General, Personal, Team, Room or Office. Empty for every type."));
        yield return ("EventTypes", WorkflowActivitySchemaBuilders.String("A comma-separated list of the event types that start the workflow, matched without regard to case. Empty for every event type."));
        yield return ("IgnoreWorkflowChanges", WorkflowActivitySchemaBuilders.Boolean("Whether changes made by a calendar workflow task are ignored, so a workflow doesn't start itself again. Defaults to true."));
    }
}

/// <summary>
/// Describes the properties every calendar workflow task shares.
/// </summary>
internal abstract class CalendarTaskActivitySchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string TextHint = " A plain string that supports Liquid.";

    protected override string Category => "Calendar";

    protected abstract string DefaultOutputName { get; }

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in GetTaskProperties())
        {
            yield return property;
        }

        yield return ("OutputName", WorkflowActivitySchemaBuilders.String(DefaultOutputName is null
            ? "Not used by this task, which writes no output."
            : $"The name of the workflow output that receives the result. Defaults to '{DefaultOutputName}'."));
    }

    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties();

    protected static JsonSchemaBuilder Text(string description)
        => WorkflowActivitySchemaBuilders.String(description + TextHint);
}

internal sealed class CalendarEventCreatedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventCreatedEvent";

    protected override string DisplayText => "Calendar Event Created";

    protected override string Description => "Starts when an event is added to a calendar";
}

internal sealed class CalendarEventUpdatedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventUpdatedEvent";

    protected override string DisplayText => "Calendar Event Updated";

    protected override string Description => "Starts when a calendar event changes";
}

internal sealed class CalendarEventCancelledEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventCancelledEvent";

    protected override string DisplayText => "Calendar Event Cancelled";

    protected override string Description => "Starts when a calendar event, or one occurrence of a repeating event, is cancelled";
}

internal sealed class CalendarEventDeletedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventDeletedEvent";

    protected override string DisplayText => "Calendar Event Deleted";

    protected override string Description => "Starts when a calendar event is deleted";
}

internal sealed class CalendarAttendeeInvitedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarAttendeeInvitedEvent";

    protected override string DisplayText => "Calendar Attendee Invited";

    protected override string Description => "Starts when a person, guest, room or piece of equipment is invited to a calendar event";
}

internal sealed class CalendarAttendeeRespondedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarAttendeeRespondedEvent";

    protected override string DisplayText => "Calendar Attendee Response Received";

    protected override string Description => "Starts when an attendee accepts, tentatively accepts or declines a calendar event";
}

internal sealed class CalendarEventCompletedEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventCompletedEvent";

    protected override string DisplayText => "Calendar Event Completed";

    protected override string Description => "Starts when a calendar event ends. Needs the Calendar Reminders feature";
}

internal sealed class CalendarEventStartingSoonEventSchema : CalendarEventActivitySchemaBase
{
    public override string Name => "CalendarEventStartingSoonEvent";

    protected override string DisplayText => "Calendar Event Starting Soon";

    protected override string Description => "Starts a set number of minutes before a calendar event starts. Needs the Calendar Reminders feature";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in base.GetPropertyDefinitions(context))
        {
            yield return property;
        }

        yield return ("MinutesBefore", WorkflowActivitySchemaBuilders.Integer("How many minutes before the start of the event the workflow starts, from 0 to 43200. Defaults to 15.").Minimum(0).Maximum(43200));
    }
}

internal sealed class CreateCalendarEventTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "CreateCalendarEventTask";

    protected override string DisplayText => "Create Calendar Event";

    protected override string Description => "Adds an event to a calendar";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["CalendarId", "Start"];

    protected override string DefaultOutputName => "CalendarEvent";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("CalendarId", Text("The identifier of the calendar the event is added to."));
        yield return ("EventType", Text("The event type, one of the calendar type's event types. The calendar type's default when empty."));
        yield return ("Title", Text("The title of the event."));
        yield return ("Start", Text("When the event starts: a date (yyyy-MM-dd) for an all-day event, an ISO 8601 time with an offset for a moment, or one without an offset for a wall-clock time in the calendar's time zone."));
        yield return ("End", Text("When the event ends, in the same forms as Start. For an all-day event, the last day. Defaults to the event type's length."));
        yield return ("Location", Text("Where the event is."));
        yield return ("Description", Text("The description of the event."));
        yield return ("Attendees", Text("The people invited, separated by commas or lines: an email address (optionally with a name, such as \"Ann <ann@example.com>\") or user:{userId} for a user of the site."));
    }
}

internal sealed class UpdateCalendarEventTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "UpdateCalendarEventTask";

    protected override string DisplayText => "Update Calendar Event";

    protected override string Description => "Changes a calendar event, or one occurrence of a repeating event. Empty fields keep their values";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["EventId"];

    protected override string DefaultOutputName => "CalendarEvent";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("EventId", Text("The identifier of the event to change."));
        yield return ("OccurrenceStart", Text("For a repeating event: the original start (ISO 8601) of the one occurrence to change. Empty changes the whole event."));
        yield return ("Title", Text("The new title."));
        yield return ("Start", Text("The new start, as an ISO 8601 time with an offset, or a wall-clock time in the event's time zone. The event keeps its length unless End is given."));
        yield return ("End", Text("The new end, in the same forms as Start."));
        yield return ("Location", Text("The new location."));
        yield return ("Description", Text("The new description."));
    }
}

internal sealed class CancelCalendarEventTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "CancelCalendarEventTask";

    protected override string DisplayText => "Cancel Calendar Event";

    protected override string Description => "Cancels a calendar event, or one occurrence of a repeating event";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["EventId"];

    protected override string DefaultOutputName => "CalendarEvent";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("EventId", Text("The identifier of the event to cancel."));
        yield return ("OccurrenceStart", Text("For a repeating event: the original start (ISO 8601) of the one occurrence to cancel. Empty cancels the whole event."));
        yield return ("Reason", Text("Why the event is cancelled, sent to the attendees."));
    }
}

internal sealed class GetCalendarEventTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "GetCalendarEventTask";

    protected override string DisplayText => "Get Calendar Event";

    protected override string Description => "Reads a calendar event into the workflow output";

    protected override IEnumerable<string> Outcomes => ["Found", "NotFound"];

    protected override IEnumerable<string> RequiredProperties => ["EventId"];

    protected override string DefaultOutputName => "CalendarEvent";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("EventId", Text("The identifier of the event to read."));
    }
}

internal sealed class FindCalendarEventsTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "FindCalendarEventsTask";

    protected override string DisplayText => "Find Calendar Events";

    protected override string Description => "Lists the event occurrences of calendars between two times";

    protected override IEnumerable<string> Outcomes => ["Found", "None"];

    protected override IEnumerable<string> RequiredProperties => ["CalendarIds", "From", "To"];

    protected override string DefaultOutputName => "CalendarEvents";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("CalendarIds", Text("The identifiers of the calendars to search, separated by commas or lines."));
        yield return ("From", Text("The start of the range, as an ISO 8601 time (UTC when it has no offset)."));
        yield return ("To", Text("The end of the range, as an ISO 8601 time (UTC when it has no offset)."));
    }
}

internal sealed class FindFreeTimeTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "FindFreeTimeTask";

    protected override string DisplayText => "Find Free Time";

    protected override string Description => "Finds the times when calendars and people are all free for a given length";

    protected override IEnumerable<string> Outcomes => ["Found", "None"];

    protected override IEnumerable<string> RequiredProperties => ["From", "To", "DurationMinutes"];

    protected override string DefaultOutputName => "FreeTimes";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("CalendarIds", Text("The identifiers of the calendars that must be free, separated by commas or lines."));
        yield return ("UserIds", Text("The identifiers of the users whose calendars must be free, separated by commas or lines."));
        yield return ("From", Text("The start of the range, as an ISO 8601 time (UTC when it has no offset)."));
        yield return ("To", Text("The end of the range, as an ISO 8601 time (UTC when it has no offset)."));
        yield return ("DurationMinutes", Text("How many minutes each free time must last, at least 1."));
    }
}

internal sealed class AddCalendarAttendeeTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "AddCalendarAttendeeTask";

    protected override string DisplayText => "Add Calendar Attendee";

    protected override string Description => "Invites people to a calendar event";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["EventId", "Attendees"];

    protected override string DefaultOutputName => null;

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("EventId", Text("The identifier of the event."));
        yield return ("Attendees", Text("The people to invite, separated by commas or lines: an email address (optionally with a name) or user:{userId} for a user of the site."));
    }
}

internal sealed class RemoveCalendarAttendeeTaskSchema : CalendarTaskActivitySchemaBase
{
    public override string Name => "RemoveCalendarAttendeeTask";

    protected override string DisplayText => "Remove Calendar Attendee";

    protected override string Description => "Removes people from a calendar event";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["EventId", "Attendees"];

    protected override string DefaultOutputName => null;

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetTaskProperties()
    {
        yield return ("EventId", Text("The identifier of the event."));
        yield return ("Attendees", Text("The people to remove, separated by commas or lines: an email address or user:{userId}."));
    }
}
