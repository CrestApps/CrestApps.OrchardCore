using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for registration settings.
/// </summary>
public sealed class RegistrationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "RegistrationSettings";

    /// <summary>
    /// Builds the schema for registration settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for user registration.")
            .Properties(
                ("UsersMustValidateEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether users must validate their email address before activation.")),
                ("UsersAreModerated", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether new user registrations require administrator approval.")),
                ("UseSiteTheme", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the site theme for the registration page.")))
            .AdditionalProperties(false);
}
