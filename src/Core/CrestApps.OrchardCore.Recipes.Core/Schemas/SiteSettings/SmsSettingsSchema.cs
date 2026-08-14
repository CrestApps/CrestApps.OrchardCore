using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for SMS settings.
/// </summary>
public sealed class SmsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SmsSettings";

    /// <summary>
    /// Builds the schema for SMS settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the SMS service.")
            .Properties(
                ("DefaultProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the default SMS provider.")))
            .AdditionalProperties(false);
}
