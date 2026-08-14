using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Layers" recipe step — defines display layers with conditional rules.
/// </summary>
public sealed class LayersRecipeStep : IRecipeStep
{
    private readonly IRuleSchemaService _ruleSchemaService;

    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayersRecipeStep"/> class.
    /// </summary>
    /// <param name="ruleSchemaService">The rule schema service used to describe the layer rule and its conditions.</param>
    public LayersRecipeStep(IRuleSchemaService ruleSchemaService)
    {
        _ruleSchemaService = ruleSchemaService;
    }

    public string Name => "Layers";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var layerRuleSchema = await _ruleSchemaService.GetLayerRuleSchemaAsync(cancellationToken);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Layers").Description("Recipe step discriminator. Must be 'Layers'.")),
                ("Layers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Layer name. Re-running the recipe with the same name updates the existing layer instead of creating a duplicate.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Administrative description of when the layer should be used.")),
                            ("Rule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String | SchemaValueType.Null)
                                .Description("A legacy JavaScript rule expression, for example isHomepage(). Prefer 'LayerRule' for new layers; this is only used when 'LayerRule' is not supplied.")),
                            ("LayerRule", layerRuleSchema))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("Layers to create or update.")))
            .Required("name", "Layers")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
