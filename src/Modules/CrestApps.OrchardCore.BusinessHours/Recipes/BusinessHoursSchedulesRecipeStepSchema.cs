using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.BusinessHours.Recipes;

/// <summary>
/// Describes the business hours schedules recipe step to CrestApps Recipes.
/// </summary>
internal sealed class BusinessHoursSchedulesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => BusinessHoursConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(BusinessHoursConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'BusinessHoursSchedules'.")),
                (BusinessHoursSchedulesRecipeStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Schedule()).Description("The schedules to create, or update by id or name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Minute(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(1440).Description(description);

    private static JsonSchemaBuilder Schedule()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", Text("The schedule id; omit to match by name.")),
                ("Name", Text("The unique schedule name.")),
                ("Description", Text("A description for administrators.")),
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the schedule is used. A disabled schedule counts around the clock.")),
                ("Hours", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("TimeZoneId", Text("The IANA time zone the hours are in, such as America/New_York. Empty means UTC.")),
                        ("Windows", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Day", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(6).Description("The day of the week, 0 for Sunday to 6 for Saturday.")),
                                    ("OpenMinute", Minute("The local opening time, in minutes from midnight.")),
                                    ("CloseMinute", Minute("The local closing time, in minutes from midnight (exclusive). The same as the opening time means all day; earlier runs past midnight.")))
                                .Required("Day", "OpenMinute", "CloseMinute"))
                            .Description("The open windows of the week; a day can have several or none.")),
                        ("DateExceptions", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Date", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The local date.")),
                                    ("Name", Text("What the date is, such as New Year's Day.")),
                                    ("Closed", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the date is closed all day; otherwise it is open only in its special hours.")),
                                    ("OpenMinute", Minute("The special opening time, in minutes from midnight.")),
                                    ("CloseMinute", Minute("The special closing time, in minutes from midnight.")))
                                .Required("Date"))
                            .Description("Holidays and days with special hours.")))
                    .Description("The hours.")))
            .Required("Name")
            .AdditionalProperties(true);
    }
}
