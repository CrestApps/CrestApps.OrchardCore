using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for email authenticator login settings.
/// </summary>
public sealed class EmailAuthenticatorLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "EmailAuthenticatorLoginSettings";

    /// <summary>
    /// Builds the schema for email authenticator login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for email-based two-factor authentication.")
            .Properties(
                ("Subject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The subject line for the verification email.")),
                ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The body template for the verification email. Use {{ Code }} for the verification code placeholder.")))
            .AdditionalProperties(false);
}
