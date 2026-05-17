using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for two-factor login settings.
/// </summary>
public sealed class TwoFactorLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TwoFactorLoginSettings";

    /// <summary>
    /// Builds the schema for two-factor login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for two-factor authentication.")
            .Properties(
                ("RequireTwoFactorAuthentication", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to require two-factor authentication for all users.")),
                ("AllowRememberClientTwoFactorAuthentication", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to remember their two-factor authentication on a device.")),
                ("NumberOfRecoveryCodesToGenerate", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The number of recovery codes to generate.").Default(5)),
                ("UseSiteTheme", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the site theme for the two-factor authentication pages.")))
            .AdditionalProperties(false);
}
