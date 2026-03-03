using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AdminMenu" recipe step — creates or updates admin menu structures.
/// </summary>
public sealed class AdminMenuRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "AdminMenu";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminMenu")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("MenuItems", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(ContentCommonSchemas.ContentItemSchema)
                                .Description("The list of menu item content items.")))
                        .Required("Id", "Name", "MenuItems")
                        .AdditionalProperties(true))))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
}
