using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the Workforce recipe step to CrestApps Recipes.
/// </summary>
internal sealed class WorkforceRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => WorkforceConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(WorkforceConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Workforce'.")),
                ("ActivityCodes", Array(ActivityCode(), "The activity codes to create, or update by id or name.")),
                ("Teams", Array(Team(), "The teams to create, or update by id or name, in any order.")),
                ("Workers", Array(Worker(), "The workers to create, or update by id, employee number or user.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(items).Description(description);
    }

    private static JsonSchemaBuilder Text(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
    }

    private static JsonSchemaBuilder Flag(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);
    }

    private static JsonSchemaBuilder ActivityCode()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The code id; omit to match by name.")),
                ("name", Text("The activity name.")),
                ("code", Text("A short code shown on schedules.")),
                ("kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Work", "Break", "Meal", "Meeting", "Training", "Coaching", "OfflineWork", "Travel", "OnCall", "TimeOff", "Other")),
                ("color", Text("The color, such as #2b8a3e.")),
                ("isPaid", Flag("Whether the time is paid.")),
                ("countsAsWork", Flag("Whether the time counts toward hours rules.")),
                ("countsTowardCoverage", Flag("Whether the worker counts toward staffing during it.")),
                ("enabled", Flag("Whether the code can be scheduled.")))
            .Required("name")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Team()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The team id; omit to match by name.")),
                ("name", Text("The team name.")),
                ("kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Organization", "Region", "Site", "Department", "Team", "Crew")),
                ("parentName", Text("The name of the team it belongs to, which may be listed after it.")),
                ("parentId", Text("The id of the team it belongs to.")),
                ("description", Text("What the team does.")),
                ("linkedRoleName", Text("A role whose members can read the team's calendar.")),
                ("timeZoneId", Text("The team's IANA time zone.")),
                ("enabled", Flag("Whether the team is in use.")))
            .Required("name")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Worker()
    {
        var membership = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("teamName", Text("The team, by name.")),
                ("teamId", Text("The team, by id.")),
                ("role", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Member", "Lead", "Scheduler", "Manager")),
                ("effectiveFrom", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date)),
                ("effectiveTo", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date)))
            .AdditionalProperties(false);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The worker id.")),
                ("displayName", Text("The worker's name.")),
                ("userId", Text("The user the worker signs in as.")),
                ("email", Text("The email address.")),
                ("employeeNumber", Text("The employee number, unique when set.")),
                ("employmentType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("FullTime", "PartTime", "Seasonal", "Temporary", "Contractor", "Subcontractor")),
                ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Active", "OnLeave", "Suspended", "Terminated")),
                ("teamMemberships", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(membership)))
            .Required("displayName")
            .AdditionalProperties(true);
    }
}
