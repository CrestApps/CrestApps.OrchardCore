using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Facebook settings.
/// </summary>
public sealed class FacebookSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "FacebookSettings";

    /// <summary>
    /// Builds the schema for Facebook settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Facebook social integration.")
            .Properties(
                ("AppId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Facebook Application ID.")),
                ("AppSecret", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Facebook Application Secret.")),
                ("FBInit", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to initialize the Facebook JavaScript SDK on the frontend.")),
                ("FBInitParams", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Additional parameters for FB.init() call.").Default("status: true,\nxfbml: true,\nautoLogAppEvents: true")),
                ("SdkJs", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the SDK JavaScript file to load.").Default("sdk.js")),
                ("Version", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Facebook Graph API version to use (e.g., v3.2).").Default("v3.2")))
            .Required("AppId", "AppSecret", "SdkJs")
            .AdditionalProperties(false);
}
