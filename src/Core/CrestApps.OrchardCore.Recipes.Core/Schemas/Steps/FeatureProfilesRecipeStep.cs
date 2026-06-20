using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "FeatureProfiles" recipe step — defines tenant feature profiles.
/// </summary>
public sealed class FeatureProfilesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "FeatureProfiles";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("FeatureProfiles").Description("Recipe step discriminator. Must be 'FeatureProfiles'.")),
                ("FeatureProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier for the feature profile.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name of the feature profile.")),
                            ("FeatureRules", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Rule", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Feature rule type identifier.")),
                                        ("Expression", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Expression evaluated by the rule.")))
                                    .Required("Rule", "Expression")
                                    .AdditionalProperties(true))
                                .Description("Rules that determine which features belong to the profile.")))
                        .AdditionalProperties(true))
                    .Description("Dictionary of tenant feature profiles keyed by their technical name.")))
            .Required("name", "FeatureProfiles")
            .AdditionalProperties(true)
            .Build();
}
