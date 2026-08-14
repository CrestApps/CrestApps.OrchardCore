using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for external login settings.
/// </summary>
public sealed class ExternalLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ExternalLoginSettings";

    /// <summary>
    /// Builds the schema for external login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for external login behavior.")
            .Properties(
                ("UseExternalProviderIfOnlyOneDefined", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to automatically redirect to the external provider if only one is configured.")),
                ("UseScriptToSyncProperties", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use a script to synchronize user properties from external providers.")),
                ("SyncPropertiesScript", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The script used to synchronize user properties from external providers.")))
            .AdditionalProperties(false);
}
