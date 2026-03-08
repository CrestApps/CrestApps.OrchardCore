using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "themes" recipe step — sets the site and admin theme.
/// </summary>
public sealed class ThemesRecipeStep : IRecipeStep
{
    private readonly IFeatureSchemaProvider _featureProvider;
    private JsonSchema _cached;
    public string Name => "themes";

    public ThemesRecipeStep(IFeatureSchemaProvider featureProvider)
    {
        _featureProvider = featureProvider;
    }

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var themeIds = await _featureProvider.GetThemeIdsAsync();

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("themes")),
                ("site", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(themeIds)
                    .Description("The theme ID to use for the front-end site.")),
                ("admin", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(themeIds)
                    .Description("The theme ID to use for the admin dashboard.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
