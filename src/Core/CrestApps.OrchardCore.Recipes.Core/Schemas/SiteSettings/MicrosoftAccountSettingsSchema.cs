using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Microsoft Account settings.
/// </summary>
public sealed class MicrosoftAccountSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "MicrosoftAccountSettings";

    /// <summary>
    /// Builds the schema for Microsoft Account settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Microsoft Account authentication.")
            .Properties(
                ("AppId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Application (client) ID from the Microsoft app registration.")),
                ("AppSecret", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The application secret (client secret) from the Microsoft app registration.")),
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path within the application's base path where the user-agent will be returned after sign-in.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens in the authentication properties.")))
            .Required("AppId", "AppSecret")
            .AdditionalProperties(false);
}
