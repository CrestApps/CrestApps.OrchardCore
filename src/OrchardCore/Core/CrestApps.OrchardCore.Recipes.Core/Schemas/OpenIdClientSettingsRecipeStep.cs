using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class OpenIdClientSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdClientSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("DisplayName", RecipeStepSchemaBuilders.String()),
            ("Authority", RecipeStepSchemaBuilders.String()),
            ("ClientId", RecipeStepSchemaBuilders.String()),
            ("ClientSecret", RecipeStepSchemaBuilders.String()),
            ("CallbackPath", RecipeStepSchemaBuilders.String()),
            ("SignedOutRedirectUri", RecipeStepSchemaBuilders.String()),
            ("SignedOutCallbackPath", RecipeStepSchemaBuilders.String()),
            ("Scopes", RecipeStepSchemaBuilders.String()),
            ("ResponseType", RecipeStepSchemaBuilders.String().Enum(
                "code",
                "code id_token",
                "code id_token token",
                "code token",
                "id_token",
                "id_token token")),
            ("ResponseMode", RecipeStepSchemaBuilders.String().Enum("form_post", "fragment", "query")),
            ("StoreExternalTokens", RecipeStepSchemaBuilders.Boolean()),
            ("Parameters", RecipeStepSchemaBuilders.Array(
                RecipeStepSchemaBuilders.Object(
                    [
                        ("Name", RecipeStepSchemaBuilders.String()),
                        ("Value", RecipeStepSchemaBuilders.String()),
                    ],
                    ["Name", "Value"]))),
        ]);
}
