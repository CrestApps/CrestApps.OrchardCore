using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Twitter settings.
/// </summary>
public sealed class TwitterSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TwitterSettings";

    /// <summary>
    /// Builds the schema for Twitter settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Twitter (X) API integration.")
            .Properties(
                ("ConsumerKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The API key (consumer key) from the Twitter developer application.")),
                ("ConsumerSecret", ProtectedCredential("Twitter API secret key (consumer secret)", "ConsumerSecretSecretName")),
                ("ConsumerSecretSecretName", SecretName("Twitter API secret key (consumer secret)")),
                ("AccessToken", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The access token for authenticating API requests.")),
                ("AccessTokenSecret", ProtectedCredential("Twitter access token secret", "AccessTokenSecretSecretName")),
                ("AccessTokenSecretSecretName", SecretName("Twitter access token secret")))
            .Required("ConsumerKey", "AccessToken")
            .AdditionalProperties(false);
}
