using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for authenticator app login settings.
/// </summary>
public sealed class AuthenticatorAppLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AuthenticatorAppLoginSettings";

    /// <summary>
    /// Builds the schema for authenticator app login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for authenticator app two-factor authentication.")
            .Properties(
                ("UseEmailAsAuthenticatorDisplayName", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the user's email as the authenticator app display name.")),
                ("TokenLength", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The length of the verification token.").Default(6)))
            .AdditionalProperties(false);
}
