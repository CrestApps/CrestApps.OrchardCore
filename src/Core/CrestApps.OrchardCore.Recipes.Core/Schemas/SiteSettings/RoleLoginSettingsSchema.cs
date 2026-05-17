using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for role login settings.
/// </summary>
public sealed class RoleLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "RoleLoginSettings";

    /// <summary>
    /// Builds the schema for role login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for role-based two-factor authentication requirements.")
            .Properties(
                ("RequireTwoFactorAuthenticationForSpecificRoles", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to require two-factor authentication for specific roles.")),
                ("Roles", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The roles that require two-factor authentication.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(false);
}
