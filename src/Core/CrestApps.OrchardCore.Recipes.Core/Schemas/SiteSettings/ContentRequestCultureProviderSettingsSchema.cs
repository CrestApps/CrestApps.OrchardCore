using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for content request culture provider settings.
/// </summary>
public sealed class ContentRequestCultureProviderSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ContentRequestCultureProviderSettings";

    /// <summary>
    /// Builds the schema for content request culture provider settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the content request culture provider.")
            .Properties(
                ("SetCookie", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to set a cookie with the determined culture.")))
            .AdditionalProperties(false);
}
