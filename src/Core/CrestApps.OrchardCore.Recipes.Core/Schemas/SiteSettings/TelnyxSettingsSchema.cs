using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Telnyx voice provider settings imported through the generic <c>Settings</c>
/// step.
/// </summary>
public sealed class TelnyxSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TelnyxSettings";

    /// <summary>
    /// Builds the schema for the Telnyx voice provider settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => PhoneProviderSettingsSchemas.Telnyx(forProviderStep: false);
}
