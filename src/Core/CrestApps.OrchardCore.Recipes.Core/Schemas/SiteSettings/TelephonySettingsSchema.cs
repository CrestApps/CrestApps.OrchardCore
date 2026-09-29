using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the telephony settings shared by every phone system provider.
/// </summary>
public sealed class TelephonySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TelephonySettings";

    /// <summary>
    /// Builds the schema for the telephony settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Telephony settings shared by every phone system provider.")
            .Properties(
                ("DefaultProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Examples("Telnyx", "Asterisk").Description("Technical name of the telephony provider used when no provider is requested explicitly.")),
                ("AllowedShortCodes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A short code the tenant may dial."))
                    .Description("Short codes the tenant may dial even though they are not international numbers, for example a carrier service code. Emergency codes are never dialable.")))
            .AdditionalProperties(false);
}
