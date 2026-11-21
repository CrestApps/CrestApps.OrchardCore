using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Lti.Recipes;

/// <summary>
/// Describes the <c>LearningExternalTools</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LearningExternalToolsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => LtiConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(LtiConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'LearningExternalTools'.")),
                ("Tools", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The tool's 26-character id; omit it to create a new one.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name authors pick the tool by.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Notes for administrators.")),
                            ("ClientId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The client id of the tool's OpenID application, imported with the OpenID application step.")),
                            ("DeploymentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The deployment id sent with every launch.")),
                            ("LoginInitiationUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The tool's OIDC login initiation URL.")),
                            ("TargetLinkUri", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The tool's default launch URL.")),
                            ("DeepLinkingUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The tool's Deep Linking URL, or null.")),
                            ("JwksUrl", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The URL of the tool's JSON Web Key Set, or null when the key set is on its OpenID application.")),
                            ("CustomParameters", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Custom parameters, one name=value per line.")),
                            ("PrivacyLevel", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("Anonymous (0), Name (1) or NameAndEmail (2).")),
                            ("AllowNamesAndRoles", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the tool may read course rosters.")),
                            ("AllowGrades", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the tool may manage line items and post scores.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the tool can be launched.")),
                            ("SubjectSalt", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The salt of the ids the tool knows people by, in base64. Keep it to keep the ids; a new one is made when it is empty.")))
                        .Required("Name", "ClientId")
                        .AdditionalProperties(true))
                    .Description("The external tools' settings to create or update.")))
            .Required("name", "Tools")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
