using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Asterisk provider settings imported through the generic <c>Settings</c> step.
/// </summary>
public sealed class AsteriskSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AsteriskSettings";

    /// <summary>
    /// Builds the schema for the Asterisk provider settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => PhoneProviderSettingsSchemas.Asterisk(forProviderStep: false);
}
