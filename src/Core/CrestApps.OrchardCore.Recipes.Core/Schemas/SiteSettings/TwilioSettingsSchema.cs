using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Twilio settings.
/// </summary>
public sealed class TwilioSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TwilioSettings";

    /// <summary>
    /// Builds the schema for Twilio settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Twilio SMS provider.")
            .Properties(
                ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the Twilio SMS provider is enabled.")),
                ("PhoneNumber", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Twilio phone number to send SMS messages from.")),
                ("AccountSID", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Twilio Account SID.")),
                ("AuthToken", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Twilio Auth Token.")))
            .Required("PhoneNumber", "AccountSID", "AuthToken")
            .AdditionalProperties(false);
}
