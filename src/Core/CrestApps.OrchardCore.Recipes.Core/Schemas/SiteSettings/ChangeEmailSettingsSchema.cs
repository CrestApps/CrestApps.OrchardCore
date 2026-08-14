using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for change email settings.
/// </summary>
public sealed class ChangeEmailSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ChangeEmailSettings";

    /// <summary>
    /// Builds the schema for change email settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the change email feature.")
            .Properties(
                ("AllowChangeEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow users to change their email address.")))
            .AdditionalProperties(false);
}
