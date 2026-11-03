using Json.Schema;

namespace CrestApps.OrchardCore.Calendars.Recipes;

/// <summary>
/// The JSON schema pieces of the Calendars recipe step: what the Calendars deployment step writes, which the step reads back.
/// Property names are camelCase and enums are written by name (a number is read too).
/// </summary>
internal static class CalendarRecipeSchemas
{
    public static JsonSchemaBuilder String(string description) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Integer(string description) => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    public static JsonSchemaBuilder Boolean(string description) => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    public static JsonSchemaBuilder DateTime(string description) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description(description);

    public static JsonSchemaBuilder Enum(string description, params string[] names) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description($"{description} One of: {string.Join(", ", names)} (or its number).");

    public static JsonSchemaBuilder Id(string noun) => String($"The {noun}'s id. When it matches a stored {noun}, that {noun} is updated; otherwise a new one is made under this id, so references to it keep working.");

    public static JsonSchemaBuilder Properties(string noun) => new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).AdditionalProperties(true).Description($"Extension data other features keep on the {noun}.");

    public static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description) => new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(items).Description(description);

    public static JsonSchemaBuilder Object(params (string Name, JsonSchemaBuilder Schema)[] properties)
    {
        var builder = new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true);

        builder.Properties(properties.ToDictionary(property => property.Name, property => property.Schema));

        return builder;
    }

    public static JsonSchemaBuilder Category() => Object(
        ("itemId", Id("category")),
        ("name", String("The category's name.")),
        ("color", String("The color of its events, such as #2f9e44.")),
        ("ownerId", String("The user it belongs to, or null for a category everyone can use.")),
        ("createdUtc", DateTime("When it was made.")));

    public static JsonSchemaBuilder Event() => Object(
        ("itemId", Id("event")),
        ("eventType", String("The kind of event, one of the calendar type's event types.")),
        ("title", String("The title.")),
        ("description", String("The description.")),
        ("location", String("Where it is.")),
        ("allDay", Boolean("Whether it lasts whole days; end is then the day after the last one.")),
        ("start", String("When it starts, as a wall-clock time in timeZoneId (no offset), such as 2026-11-03T09:00:00.")),
        ("end", String("When it ends, as a wall-clock time in timeZoneId.")),
        ("timeZoneId", String("The IANA time zone of start and end, such as America/New_York.")),
        ("status", Enum("The status.", "Confirmed", "Tentative", "Cancelled")),
        ("transparency", Enum("Whether it takes the time.", "Busy", "Free")),
        ("visibility", Enum("Who sees its details.", "Default", "Public", "Private")),
        ("color", String("Its own color, or null for its category's, kind's or calendar's.")),
        ("categoryId", String("The id of its category (see Categories), or null.")),
        ("reminderMinutes", Array(new JsonSchemaBuilder().Type(SchemaValueType.Integer), "Minutes before the start its people are reminded: 0, 5, 10, 15, 30, 60, 120 or 1440.")),
        ("attendees", Array(Object(
            ("userId", String("A user of the site.")),
            ("email", String("An email address, for a guest.")),
            ("name", String("The attendee's name.")),
            ("resourceCalendarId", String("A room or equipment calendar booked for the event.")),
            ("role", Enum("The attendee's role.", "Required", "Optional", "Organizer", "Resource")),
            ("response", Enum("The attendee's answer.", "NeedsAction", "Accepted", "Tentative", "Declined"))), "The people, guests, rooms and equipment invited.")),
        ("recurrence", Object(
            ("rule", String("An iCalendar RRULE, such as FREQ=WEEKLY;BYDAY=TU,WE,TH,FR,SA;UNTIL=20261130T235959Z.")),
            ("exceptionDates", Array(new JsonSchemaBuilder().Type(SchemaValueType.String), "Wall-clock starts of occurrences that are skipped.")))),
        ("seriesId", String("For a changed occurrence of a repeating event: the series it belongs to.")),
        ("originalStartUtc", DateTime("For a changed occurrence: the UTC start it replaces.")),
        ("source", String("The feature that manages the event, such as Scheduling, or null.")),
        ("sourceId", String("The record of that feature the event belongs to.")),
        ("editable", Boolean("Whether it can be changed on the calendar.")),
        ("uid", String("Its iCalendar UID.")),
        ("properties", Properties("event")));

    public static JsonSchemaBuilder Calendar() => Object(
        ("itemId", Id("calendar")),
        ("type", String("The calendar type, such as General, Personal, Team, Room, Equipment or Office; the feature that registers it must be on.")),
        ("name", String("The name.")),
        ("description", String("The description.")),
        ("color", String("The color of its events.")),
        ("timeZoneId", String("Its IANA time zone.")),
        ("ownerKind", Enum("What owns it.", "None", "User", "Role", "Resource")),
        ("ownerId", String("The user id or role name that owns it.")),
        ("visibility", Enum("Who can see it.", "Private", "Shared", "Public")),
        ("shares", Array(Object(
            ("principalKind", Enum("Who it is shared with.", "User", "Role")),
            ("principalId", String("The user id or role name.")),
            ("displayName", String("The name shown.")),
            ("access", Enum("What they can do.", "None", "FreeBusy", "Read", "Write", "Manage"))), "Who else can use it.")),
        ("conflictPolicy", Enum("Whether events may overlap.", "AllowOverlaps", "LimitConcurrent")),
        ("capacity", Integer("With LimitConcurrent: how many events may overlap.")),
        ("enabled", Boolean("Whether it is in use.")),
        ("source", String("The feature that manages it, or null.")),
        ("sourceId", String("The record of that feature it belongs to.")),
        ("properties", Properties("calendar")),
        ("events", Array(Event(), "Its events (when the deployment step included them).")));
}
