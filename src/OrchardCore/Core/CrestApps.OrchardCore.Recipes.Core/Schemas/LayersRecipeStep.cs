using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Layers" recipe step — defines display layers with conditional rules.
/// </summary>
public sealed class LayersRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Layers";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Layers")),
                ("Layers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Rule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("A JavaScript rule expression, e.g. isHomepage().")),
                            ("LayerRule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("ConditionId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("Conditions", new JsonSchemaBuilder()
                                        .Type(SchemaValueType.Array)
                                        .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
                                .AdditionalProperties(true)
                                .Description("Structured layer rule object.")))
                        .Required("Name")
                        .AdditionalProperties(true))))
            .Required("name", "Layers")
            .AdditionalProperties(true)
            .Build();
}
