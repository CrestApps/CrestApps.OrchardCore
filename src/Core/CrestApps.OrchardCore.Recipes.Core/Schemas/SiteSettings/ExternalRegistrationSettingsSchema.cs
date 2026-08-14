using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for external registration settings.
/// </summary>
public sealed class ExternalRegistrationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ExternalRegistrationSettings";

    /// <summary>
    /// Builds the schema for external registration settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for external authentication registration behavior.")
            .Properties(
                ("DisableNewRegistrations", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disable new user registrations via external providers.")),
                ("NoPassword", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to skip requiring a password for externally authenticated users.")),
                ("NoUsername", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to skip requiring a username for externally authenticated users.")),
                ("NoEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to skip requiring an email for externally authenticated users.")),
                ("UseScriptToGenerateUsername", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use a script to generate usernames for externally authenticated users.")),
                ("GenerateUsernameScript", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The script used to generate usernames for externally authenticated users.")))
            .AdditionalProperties(false);
}
