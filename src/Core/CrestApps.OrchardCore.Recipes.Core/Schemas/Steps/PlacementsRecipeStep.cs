using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Placements" recipe step — updates display and editor placement rules.
/// </summary>
public sealed class PlacementsRecipeStep : IRecipeStep
{
    private readonly IPlacementSchemaService _placementSchemaService;

    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => "Placements";

    /// <summary>
    /// Initializes a new instance of the <see cref="PlacementsRecipeStep"/> class.
    /// </summary>
    /// <param name="placementSchemaService">The service that composes the placement node schema.</param>
    public PlacementsRecipeStep(IPlacementSchemaService placementSchemaService)
    {
        _placementSchemaService = placementSchemaService;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var placementNodeSchema = await _placementSchemaService.GetPlacementNodeSchemaAsync(cancellationToken);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("Placements")
                    .Description("Recipe step discriminator. Must be 'Placements'.")),
                ("Placements", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .Items(placementNodeSchema))
                    .Description("A dictionary keyed by shape type. Each value is an array of placement nodes applied to that shape.")))
            .Required("name", "Placements")
            .AdditionalProperties(true)
            .Build();
    }
}
