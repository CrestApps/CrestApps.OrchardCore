using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for email settings.
/// </summary>
public sealed class EmailSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "EmailSettings";

    /// <summary>
    /// Builds the schema for email settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the email service.")
            .Properties(
                ("DefaultProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the default email provider.")))
            .AdditionalProperties(false);
}
