using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Azure SMS settings.
/// </summary>
public sealed class AzureSmsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AzureSmsSettings";

    /// <summary>
    /// Builds the schema for Azure SMS settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Azure Communication Services SMS provider.")
            .Properties(
                ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the Azure SMS provider is enabled.")),
                ("ConnectionString", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Azure Communication Services connection string.")),
                ("PhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The phone number to send SMS messages from.")))
            .Required("ConnectionString", "PhoneNumber")
            .AdditionalProperties(false);
}
