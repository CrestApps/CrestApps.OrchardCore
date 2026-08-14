using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the open id client settings recipe step.
/// </summary>
public sealed class OpenIdClientSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdClientSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("DisplayName", RecipeStepSchemaBuilders.String().Description("Display caption shown for the OpenID client login option.")),
                ("Authority", RecipeStepSchemaBuilders.String().Description("Authority or issuer base URL of the external OpenID provider.")),
                ("ClientId", RecipeStepSchemaBuilders.String().Description("Registered OpenID client identifier.")),
                ("ClientSecret", RecipeStepSchemaBuilders.String().Description("Registered OpenID client secret.")),
                ("CallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that the provider redirects back to after sign-in.")),
                ("SignedOutRedirectUri", RecipeStepSchemaBuilders.String().Description("Absolute URI the provider should redirect to after sign-out completes.")),
                ("SignedOutCallbackPath", RecipeStepSchemaBuilders.String().Description("Relative callback path that handles the post-logout response from the provider.")),
                ("Scopes", RecipeStepSchemaBuilders.String().Description("Space-separated list of scopes requested from the provider.")),
                ("ResponseType", RecipeStepSchemaBuilders.String().Enum(
                    "code",
                    "code id_token",
                    "code id_token token",
                    "code token",
                    "id_token",
                    "id_token token").Description("OpenID Connect response type sent to the provider.")),
                ("ResponseMode", RecipeStepSchemaBuilders.String().Enum("form_post", "fragment", "query").Description("How the provider should return the response payload.")),
                ("StoreExternalTokens", RecipeStepSchemaBuilders.Boolean().Description("Whether external access and refresh tokens should be stored for later use.")),
                ("Parameters", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("Name", RecipeStepSchemaBuilders.String().Description("Additional protocol parameter name.")),
                            ("Value", RecipeStepSchemaBuilders.String().Description("Additional protocol parameter value.")),
                        ],
                        ["Name", "Value"]).Description("Additional protocol parameters appended to the authentication request."))),
            ]);
}
