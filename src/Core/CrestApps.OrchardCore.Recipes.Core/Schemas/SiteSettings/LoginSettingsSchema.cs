using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for login settings.
/// </summary>
public sealed class LoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "LoginSettings";

    /// <summary>
    /// Builds the schema for login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for user login behavior.")
            .Properties(
                ("UseSiteTheme", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the site theme for the login page.")),
                ("DisableLocalLogin", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disable local username/password login.")),
                ("AllowChangingUsername", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to change their username.")),
                ("AllowChangingEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to change their email address.")),
                ("AllowChangingPhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to change their phone number.").Default(true)))
            .AdditionalProperties(false);
}
