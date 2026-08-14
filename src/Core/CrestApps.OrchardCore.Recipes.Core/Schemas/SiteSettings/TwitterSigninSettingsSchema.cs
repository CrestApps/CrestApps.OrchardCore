using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Twitter sign-in settings.
/// </summary>
public sealed class TwitterSigninSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TwitterSigninSettings";

    /// <summary>
    /// Builds the schema for Twitter sign-in settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for X (Twitter) sign-in authentication.")
            .Properties(
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path where the user-agent will be returned after authentication.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens.")))
            .AdditionalProperties(false);
}
