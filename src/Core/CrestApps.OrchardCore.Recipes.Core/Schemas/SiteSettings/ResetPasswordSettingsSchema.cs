using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for reset password settings.
/// </summary>
public sealed class ResetPasswordSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ResetPasswordSettings";

    /// <summary>
    /// Builds the schema for reset password settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the password reset feature.")
            .Properties(
                ("AllowResetPassword", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to reset their password.")),
                ("UseSiteTheme", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the site theme for the reset password page.")))
            .AdditionalProperties(false);
}
