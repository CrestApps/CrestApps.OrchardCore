using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Layers" recipe step — defines display layers with conditional rules.
/// </summary>
public sealed class LayersRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Layers";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Layers").Description("Recipe step discriminator. Must be 'Layers'.")),
                ("Layers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Layer name.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Administrative description of when the layer should be used.")),
                            ("Rule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("A JavaScript rule expression, e.g. isHomepage().")),
                            ("LayerRule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Rule name shown in the layer editor.")),
                                    ("ConditionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Condition provider identifier used to evaluate the rule.")),
                                    ("Conditions", new JsonSchemaBuilder()
                                        .Type(SchemaValueType.Array)
                                        .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
                                .AdditionalProperties(true)
                                .Description("Structured layer rule object.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("Layers to create or update.")))
            .Required("name", "Layers")
            .AdditionalProperties(true)
            .Build();
}
