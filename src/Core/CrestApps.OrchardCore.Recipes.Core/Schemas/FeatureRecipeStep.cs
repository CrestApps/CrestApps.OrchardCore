namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "feature" recipe step — enables or disables Orchard Core features.
/// </summary>
public sealed class FeatureRecipeStep : IRecipeStep
{
    private readonly IFeatureSchemaProvider _featureProvider;
    private JsonSchema _cached;
    public string Name => "feature";

    public FeatureRecipeStep(IFeatureSchemaProvider featureProvider)
    {
        _featureProvider = featureProvider;
    }

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var featureIds = await _featureProvider.GetFeatureIdsAsync();

        var featureItemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(featureIds);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("feature")),
                ("enable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(featureItemSchema)
                    .Description("Feature IDs to enable.")),
                ("disable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(featureItemSchema)
                    .Description("Feature IDs to disable.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
