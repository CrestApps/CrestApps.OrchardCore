using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Azure email settings.
/// </summary>
public sealed class AzureEmailSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AzureEmailSettings";

    /// <summary>
    /// Builds the schema for Azure email settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Azure Communication Services email provider.")
            .Properties(
                ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the Azure email provider is enabled.")),
                ("DefaultSender", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The default sender email address.")),
                ("ConnectionString", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Azure Communication Services connection string.")))
            .Required("ConnectionString")
            .AdditionalProperties(false);
}
