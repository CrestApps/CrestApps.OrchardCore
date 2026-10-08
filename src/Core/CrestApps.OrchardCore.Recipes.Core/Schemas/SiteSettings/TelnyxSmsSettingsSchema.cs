using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Telnyx SMS provider settings imported through the generic <c>Settings</c>
/// step.
/// </summary>
public sealed class TelnyxSmsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TelnyxSmsSettings";

    /// <summary>
    /// Builds the schema for the Telnyx SMS provider settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => PhoneProviderSettingsSchemas.TelnyxSms(forProviderStep: false);
}
