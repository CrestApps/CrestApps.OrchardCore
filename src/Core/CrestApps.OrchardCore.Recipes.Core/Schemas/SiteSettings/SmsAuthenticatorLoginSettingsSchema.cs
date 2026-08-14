using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for SMS authenticator login settings.
/// </summary>
public sealed class SmsAuthenticatorLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SmsAuthenticatorLoginSettings";

    /// <summary>
    /// Builds the schema for SMS authenticator login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for SMS-based two-factor authentication.")
            .Properties(
                ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The body template for the verification SMS message.")))
            .AdditionalProperties(false);
}
