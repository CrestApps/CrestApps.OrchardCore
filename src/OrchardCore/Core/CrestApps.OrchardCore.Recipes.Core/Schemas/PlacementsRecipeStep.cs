using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Placements" recipe step — updates display/editor placement rules.
/// </summary>
public sealed class PlacementsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Placements";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Placements")),
                ("Placements", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by shape type. Each value is an array of placement objects with 'place' and optional filters.")))
            .Required("name", "Placements")
            .AdditionalProperties(true)
            .Build();
}
