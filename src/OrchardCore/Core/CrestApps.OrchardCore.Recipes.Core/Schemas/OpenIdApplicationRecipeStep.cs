using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class OpenIdApplicationRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdApplication";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("ClientId", RecipeStepSchemaBuilders.String()),
            ("DisplayName", RecipeStepSchemaBuilders.String()),
            ("RedirectUris", RecipeStepSchemaBuilders.String()),
            ("PostLogoutRedirectUris", RecipeStepSchemaBuilders.String()),
            ("Type", RecipeStepSchemaBuilders.String().Enum("confidential", "public")),
            ("ConsentType", RecipeStepSchemaBuilders.String().Enum("explicit", "external", "implicit")),
            ("ClientSecret", RecipeStepSchemaBuilders.String()),
            ("RoleEntries", RecipeStepSchemaBuilders.Array(
                RecipeStepSchemaBuilders.Object(
                    [
                        ("Name", RecipeStepSchemaBuilders.String()),
                    ],
                    ["Name"]))),
            ("ScopeEntries", RecipeStepSchemaBuilders.Array(
                RecipeStepSchemaBuilders.Object(
                    [
                        ("Name", RecipeStepSchemaBuilders.String()),
                    ],
                    ["Name"]))),
            ("AllowPasswordFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowClientCredentialsFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowAuthorizationCodeFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowRefreshTokenFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowHybridFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowImplicitFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowLogoutEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("AllowIntrospectionEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("AllowRevocationEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("RequireProofKeyForCodeExchange", RecipeStepSchemaBuilders.Boolean()),
            ("RequirePushedAuthorizationRequests", RecipeStepSchemaBuilders.Boolean()),
        ]);
}
