using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterBusinessHoursCalendar" recipe step — imports reusable business-hours calendars that gate when queues and entry points route work.
/// </summary>
public sealed class ContactCenterBusinessHoursCalendarRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterBusinessHoursCalendar";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterBusinessHoursCalendar").Description("Recipe step discriminator. Must be 'ContactCenterBusinessHoursCalendar'.")),
                ("Calendars", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing calendar is updated instead of a new one being created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the calendar.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the calendar.")),
                            ("TimeZoneId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Time zone the weekly schedule and holidays are evaluated in. When empty, UTC is used.")),
                            ("WeeklySchedule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Day", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday").Description("The day of the week this window applies to.")),
                                        ("IsOpen", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the queue is open on this day.")),
                                        ("OpenMinute", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Local time, in minutes from midnight, the open window starts.")),
                                        ("CloseMinute", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Local time, in minutes from midnight (exclusive), the open window ends.")))
                                    .AdditionalProperties(true))
                                .Description("Per-day open windows that define the weekly schedule.")),
                            ("Holidays", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A closed date in 'yyyy-MM-dd' format."))
                                .Description("Dates the queue is closed all day regardless of the weekly schedule.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the calendar is enabled. Disabled calendars do not gate routing.")))
                        .AdditionalProperties(true))
                    .Description("The Contact Center business-hours calendars to create or update.")))
            .Required("name", "Calendars")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
