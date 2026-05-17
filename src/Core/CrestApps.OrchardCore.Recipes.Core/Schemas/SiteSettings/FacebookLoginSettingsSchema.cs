using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Facebook Login settings.
/// </summary>
public sealed class FacebookLoginSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "FacebookLoginSettings";

    /// <summary>
    /// Builds the schema for Facebook Login settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Facebook Login authentication.")
            .Properties(
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path within the application's base path where the user-agent will be returned after sign-in from Facebook.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens in the authentication properties.")))
            .AdditionalProperties(false);
}
