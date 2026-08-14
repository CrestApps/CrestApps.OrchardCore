using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for reCAPTCHA settings.
/// </summary>
public sealed class ReCaptchaSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ReCaptchaSettings";

    /// <summary>
    /// Builds the schema for reCAPTCHA settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Google reCAPTCHA.")
            .Properties(
                ("SiteKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The reCAPTCHA site key.")),
                ("SecretKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The reCAPTCHA secret key.")),
                ("ReCaptchaScriptUri", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI for the reCAPTCHA JavaScript script.")),
                ("ReCaptchaApiUri", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI for the reCAPTCHA verification API.")))
            .Required("SiteKey", "SecretKey")
            .AdditionalProperties(false);
}
