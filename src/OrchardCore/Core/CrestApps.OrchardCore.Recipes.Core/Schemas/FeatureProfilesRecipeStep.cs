using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "FeatureProfiles" recipe step — defines tenant feature profiles.
/// </summary>
public sealed class FeatureProfilesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "FeatureProfiles";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("FeatureProfiles")),
                ("FeatureProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("FeatureRules", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Rule", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("Expression", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                                    .Required("Rule", "Expression")
                                    .AdditionalProperties(true))))
                        .AdditionalProperties(true))))
            .Required("name", "FeatureProfiles")
            .AdditionalProperties(true)
            .Build();
}
