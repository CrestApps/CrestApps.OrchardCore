using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "TelnyxSettings" recipe step — imports the Telnyx voice provider settings. Secrets are taken in clear
/// text and protected on import, are never exported, and keep their stored value when omitted.
/// </summary>
public sealed class TelnyxSettingsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "TelnyxSettings";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => PhoneProviderSettingsRecipeSteps.Build(Name, PhoneProviderSettingsSchemas.Telnyx(forProviderStep: true));
}

/// <summary>
/// Schema for the "TelnyxSmsSettings" recipe step — imports the Telnyx SMS provider settings. Secrets are taken in clear
/// text and protected on import, are never exported, and keep their stored value when omitted.
/// </summary>
public sealed class TelnyxSmsSettingsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "TelnyxSmsSettings";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => PhoneProviderSettingsRecipeSteps.Build(Name, PhoneProviderSettingsSchemas.TelnyxSms(forProviderStep: true));
}

/// <summary>
/// Schema for the "AsteriskSettings" recipe step — imports the Asterisk provider settings. Secrets are taken in clear
/// text and protected on import, are never exported, and keep their stored value when omitted.
/// </summary>
public sealed class AsteriskSettingsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "AsteriskSettings";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => PhoneProviderSettingsRecipeSteps.Build(Name, PhoneProviderSettingsSchemas.Asterisk(forProviderStep: true));
}

internal static class PhoneProviderSettingsRecipeSteps
{
    /// <summary>
    /// Builds a provider settings step whose single <c>Settings</c> property holds the settings object.
    /// </summary>
    /// <param name="stepName">The recipe step name.</param>
    /// <param name="settings">The schema of the settings object.</param>
    public static JsonSchema Build(string stepName, JsonSchemaBuilder settings)
        => RecipeStepSchemaBuilders.BuildNamedStep(
            stepName,
            [
                ("Settings", settings),
            ],
            ["Settings"]);
}
