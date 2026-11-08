using System.Reflection;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.TimeTracker.Recipes;

/// <summary>
/// Describes the TimeTracker recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TimeTrackerRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TimeTrackerConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var category = Object(
            "A time category: what time counts toward, such as paid or billable time.",
            ("ItemId", Text("The category's id. When it matches a stored category, that category is updated; otherwise it is matched by name, and a new one keeps this id.")),
            ("name", Text("The category name, unique.")),
            ("code", Text("A short code.")),
            ("color", Text("The color, such as #2b8a3e.")),
            ("position", Integer("Where it is listed.")),
            ("factor", Number("How a minute counts toward the category. Defaults to 1.")),
            ("enabled", Flag("Whether the category is in use.")))
            .Required("name");

        var stage = Object(
            "A time stage: what a person is doing, such as working or on a break.",
            ("ItemId", Text("The stage's id. When it matches a stored stage, that stage is updated; otherwise it is matched by name, and a new one keeps this id so activity codes that point at it still do.")),
            ("name", Text("The stage name, unique.")),
            ("code", Text("A short code.")),
            ("color", Text("The color, such as #2b8a3e.")),
            ("position", Integer("Where it is listed.")),
            ("selfService", Flag("Whether people can switch to it themselves.")),
            ("enabled", Flag("Whether the stage is in use.")),
            ("categories", Strings("The names of the categories its time counts toward. When present, they replace the stage's categories; a name that doesn't match a category is reported and skipped.")))
            .Required("name");

        var entry = Object(
            "A time entry. It is skipped when the person already has time in that range.",
            ("userName", Text("The user name of the person; the user must exist.")),
            ("stage", Text("The name of the stage; it must exist or be listed in Stages.")),
            ("startUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When the entry starts, in UTC.")),
            ("endUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When the entry ends, in UTC.")),
            ("source", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Examples([.. typeof(TimeTrackerConstants.Sources)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                    .Select(field => (System.Text.Json.Nodes.JsonNode)(string)field.GetRawConstantValue())])
                .Description($"Where the entry came from. Defaults to {TimeTrackerConstants.Sources.Import}.")),
            ("note", Text("A note.")))
            .Required("userName", "stage", "startUtc", "endUtc");

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates time categories and stages, matched by ItemId then name, and adds time entries. A member left out of a category or stage keeps its stored value.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TimeTrackerConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'TimeTracker'.")),
                ("Categories", Array(category, "The time categories to create or update.")),
                ("Stages", Array(stage, "The time stages to create or update. Categories are imported first, so a stage can name a category of the same step.")),
                ("Entries", Array(entry, "The time entries to add.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Flag(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);

    private static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number).Description(description);

    private static JsonSchemaBuilder Strings(string description)
        => Array(new JsonSchemaBuilder().Type(SchemaValueType.String), description);

    private static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(items).Description(description);

    private static JsonSchemaBuilder Object(string description, params (string Name, JsonSchemaBuilder Schema)[] properties)
        => new JsonSchemaBuilder().Type(SchemaValueType.Object).Description(description).Properties(properties).AdditionalProperties(true);
}
