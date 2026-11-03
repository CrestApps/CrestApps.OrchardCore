using Json.Schema;

namespace CrestApps.OrchardCore.Scheduling.Recipes;

/// <summary>
/// The JSON schema pieces of the Scheduling recipe step: what the Scheduling deployment step writes, which the step reads back.
/// Property names are camelCase and enums are written by name (a number is read too).
/// </summary>
internal static class SchedulingRecipeSchemas
{
    public static JsonSchemaBuilder String(string description) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder Integer(string description) => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    public static JsonSchemaBuilder Boolean(string description) => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    public static JsonSchemaBuilder DateTime(string description) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description(description);

    public static JsonSchemaBuilder Enum(string description, params string[] names) => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description($"{description} One of: {string.Join(", ", names)} (or its number).");

    public static JsonSchemaBuilder Id(string noun) => String($"The {noun}'s id. When it matches a stored {noun}, that {noun} is replaced; otherwise a new one is made under this id, so references to it keep working.");

    public static JsonSchemaBuilder Strings(string description) => new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description(description);

    public static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description) => new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(items).Description(description);

    public static JsonSchemaBuilder Object(params (string Name, JsonSchemaBuilder Schema)[] properties)
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true)
            .Properties(properties.ToDictionary(property => property.Name, property => property.Schema));
    }

    public static JsonSchemaBuilder Resource() => Object(
        ("itemId", Id("resource")),
        ("name", String("The name.")),
        ("kind", Enum("What it is.", "Staff", "Room", "Equipment", "Other")),
        ("userId", String("For staff: the user.")),
        ("calendarId", String("The calendar that holds its appointments (see the Calendars step).")),
        ("timeZoneId", String("The IANA time zone of its working hours.")),
        ("availabilityMode", Enum("Where its hours come from.", "WorkingHours", "Shifts")),
        ("shiftCalendarId", String("With Shifts: the calendar whose Shift events name it.")),
        ("workingHours", Array(Object(
            ("day", Enum("The day.", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday")),
            ("start", String("When it starts, as HH:mm.")),
            ("end", String("When it ends, as HH:mm."))), "Its weekly hours; several on a day leave the gaps as breaks.")),
        ("dateOverrides", Array(Object(
            ("date", DateTime("The day.")),
            ("closed", Boolean("Whether it is closed that day.")),
            ("start", String("Otherwise, when it opens, as HH:mm.")),
            ("end", String("And when it closes, as HH:mm."))), "Days with other hours.")),
        ("enabled", Boolean("Whether it can be booked.")),
        ("properties", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).AdditionalProperties(true).Description("Extension data other features keep on the resource.")));

    public static JsonSchemaBuilder AppointmentType() => Object(
        ("itemId", Id("appointment type")),
        ("name", String("The name.")),
        ("description", String("The description.")),
        ("slug", String("The address of its public booking page (book/{slug}).")),
        ("durationMinutes", Integer("How long an appointment lasts.")),
        ("slotIntervalMinutes", Integer("How far apart the start times are.")),
        ("bufferBeforeMinutes", Integer("Free time kept before each appointment.")),
        ("bufferAfterMinutes", Integer("Free time kept after each appointment.")),
        ("minimumNoticeMinutes", Integer("How long before an appointment it can still be booked.")),
        ("horizonDays", Integer("How far ahead it can be booked.")),
        ("maxPerDay", Integer("The most a resource takes a day; 0 for no limit.")),
        ("maxPerWeek", Integer("The most a resource takes a week (Monday to Sunday in its time zone); 0 for no limit.")),
        ("maxUpcomingPerCustomer", Integer("The most upcoming appointments of this type one customer can book themselves; 0 for no limit.")),
        ("cancelNoticeMinutes", Integer("How soon before the start a customer can still cancel it themselves; 0 for any time.")),
        ("rescheduleNoticeMinutes", Integer("How soon before the start a customer can still move it themselves; 0 for any time.")),
        ("capacity", Integer("How many people one appointment takes (more than 1 for group sessions).")),
        ("resourceIds", Strings("The resources that can take it. With resourceRequirements, every resource of every requirement.")),
        ("assignment", Enum("How a resource is chosen, when there are no resourceRequirements.", "AnyAvailable", "RoundRobin", "AllTogether")),
        ("resourceRequirements", Array(Object(
            ("name", String("The group's name, such as Staff or Room.")),
            ("resourceIds", Strings("The resources in the group, in order of preference.")),
            ("assignment", Enum("How the group's resources are picked.", "AnyAvailable", "RoundRobin", "AllTogether")),
            ("weights", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("Round-robin weights by resource id; a resource with 2 takes about twice as many appointments as one with 1, the default."))),
            "Groups of resources each appointment needs, such as one person from the staff and one room; a time is offered only when every group can be met. When empty, resourceIds and assignment make the only group.")),
        ("requiresConfirmation", Boolean("Whether bookings wait for someone to confirm them.")),
        ("holdMinutes", Integer("How long a held time is kept.")),
        ("allowSelfService", Boolean("Whether customers can book it themselves.")),
        ("location", String("Where it happens.")),
        ("color", String("The color of its appointments.")),
        ("enabled", Boolean("Whether it can be booked.")));

    public static JsonSchemaBuilder Appointment() => Object(
        ("itemId", Id("appointment")),
        ("appointmentTypeId", String("Its appointment type.")),
        ("resourceIds", Strings("The resources it is with.")),
        ("startUtc", DateTime("When it starts.")),
        ("endUtc", DateTime("When it ends.")),
        ("timeZoneId", String("The customer's time zone.")),
        ("status", Enum("Its status.", "Held", "Requested", "Confirmed", "Completed", "NoShow", "Cancelled", "Expired")),
        ("holdExpiresUtc", DateTime("When a held time is let go.")),
        ("customerName", String("The customer's name.")),
        ("customerEmail", String("The customer's email.")),
        ("customerPhone", String("The customer's phone.")),
        ("userId", String("The customer's user, if they have one.")),
        ("notes", String("Notes.")),
        ("calendarEventId", String("Its calendar event (see the Calendars step).")),
        ("source", String("Where it was booked.")),
        ("cancelReason", String("Why it was cancelled.")),
        ("createdUtc", DateTime("When it was made.")),
        ("modifiedUtc", DateTime("When it last changed.")));

    public static JsonSchemaBuilder WaitlistEntry() => Object(
        ("itemId", Id("waitlist entry")),
        ("appointmentTypeId", String("The appointment type waited for.")),
        ("fromUtc", DateTime("The start of the times wanted.")),
        ("toUtc", DateTime("The end of the times wanted.")),
        ("customerName", String("The customer's name.")),
        ("customerEmail", String("The customer's email.")),
        ("customerPhone", String("The customer's phone.")),
        ("status", Enum("Its status.", "Waiting", "Notified", "Booked", "Removed")),
        ("createdUtc", DateTime("When it was added.")),
        ("notifiedUtc", DateTime("When the customer was told a time opened.")));
}
