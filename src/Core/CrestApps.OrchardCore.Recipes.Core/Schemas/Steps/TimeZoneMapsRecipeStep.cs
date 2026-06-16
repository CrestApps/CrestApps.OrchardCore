using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "TimeZoneMaps" recipe step.
/// </summary>
public sealed class TimeZoneMapsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc />
    public override string Name => "TimeZoneMaps";

    protected override JsonSchema CreateSchema()
    {
        var mapSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique friendly time zone name.")),
                ("TimeZoneId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("IANA time zone identifier to store for this map.")),
                ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The user id of the user creating the entry. Leave it blank to use the current user's Id.")),
                ("Author", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The username of the user creating the entry. Leave it blank to use the current username.")))
            .Required("Name", "TimeZoneId")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("TimeZoneMaps")),
                ("Maps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(mapSchema)
                    .MinItems(1)
                    .Description("The time zone maps to create or update.")))
            .Required("name", "Maps")
            .AdditionalProperties(true)
            .Build();
    }
}


