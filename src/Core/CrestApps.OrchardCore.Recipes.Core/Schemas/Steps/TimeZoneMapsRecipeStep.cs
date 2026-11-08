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
                ("ItemId", CatalogRecipeStepSchemas.ItemId("map")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique friendly time zone name. A map whose ItemId matches no stored map is matched by this name, so a second run updates it.")),
                ("TimeZoneId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("IANA time zone identifier to store for this map.")))
            .Description("A time zone map. A deployment plan also writes CreatedUtc, ModifiedUtc, OwnerId and Author; the import ignores them and stamps the importing site's own audit values.")
            .Required("Name", "TimeZoneId")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("TimeZoneMaps").Description("Recipe step discriminator. Must be 'TimeZoneMaps'.")),
                ("Maps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(mapSchema)
                    .MinItems(1)
                    .Description("The time zone maps to create, or to update when their ItemId or Name matches a stored map.")))
            .Required("name", "Maps")
            .AdditionalProperties(true)
            .Build();
    }
}
