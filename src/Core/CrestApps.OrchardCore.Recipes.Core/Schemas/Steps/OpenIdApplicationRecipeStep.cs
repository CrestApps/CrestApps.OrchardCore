using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the open id application recipe step.
/// </summary>
public sealed class OpenIdApplicationRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdApplication";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("ClientId", RecipeStepSchemaBuilders.String().Description("Unique OpenID application client identifier.")),
                ("DisplayName", RecipeStepSchemaBuilders.String().Description("Human-readable application name shown to users and administrators.")),
                ("RedirectUris", RecipeStepSchemaBuilders.String().Description("Space-separated or serialized list of allowed redirect URIs.")),
                ("PostLogoutRedirectUris", RecipeStepSchemaBuilders.String().Description("Space-separated or serialized list of allowed post-logout redirect URIs.")),
                ("Type", RecipeStepSchemaBuilders.String().Enum("confidential", "public").Description("OAuth client type.")),
                ("ConsentType", RecipeStepSchemaBuilders.String().Enum("explicit", "external", "implicit").Description("How user consent is handled for the application.")),
                ("ClientSecret", RecipeStepSchemaBuilders.String().Description("Client secret used by confidential applications.")),
                ("RoleEntries", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("Name", RecipeStepSchemaBuilders.String().Description("Role name granted to the application.")),
                        ],
                        ["Name"]).Description("Roles granted to the client application."))),
                ("ScopeEntries", RecipeStepSchemaBuilders.Array(
                    RecipeStepSchemaBuilders.Object(
                        [
                            ("Name", RecipeStepSchemaBuilders.String().Description("Scope name granted to the application.")),
                        ],
                        ["Name"]).Description("Scopes granted to the client application."))),
                ("AllowPasswordFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the resource owner password flow is allowed for this client.")),
                ("AllowClientCredentialsFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the client credentials flow is allowed for this client.")),
                ("AllowAuthorizationCodeFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the authorization code flow is allowed for this client.")),
                ("AllowRefreshTokenFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the client can request refresh tokens.")),
                ("AllowHybridFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether hybrid response types are allowed for this client.")),
                ("AllowImplicitFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether implicit response types are allowed for this client.")),
                ("AllowLogoutEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the client can use the logout endpoint.")),
                ("AllowIntrospectionEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the client can call the introspection endpoint.")),
                ("AllowRevocationEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the client can call the revocation endpoint.")),
                ("RequireProofKeyForCodeExchange", RecipeStepSchemaBuilders.Boolean().Description("Whether PKCE is required for authorization code requests.")),
                ("RequirePushedAuthorizationRequests", RecipeStepSchemaBuilders.Boolean().Description("Whether the client must use pushed authorization requests.")),
            ]);
}
