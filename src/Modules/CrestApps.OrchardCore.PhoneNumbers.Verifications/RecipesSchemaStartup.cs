using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications;

/// <summary>
/// Describes the phone number verification site settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, PhoneNumberVerificationsSettingsSchema>();
    }
}

/// <summary>
/// Describes the AbstractAPI provider settings to CrestApps Recipes.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.AbstractApi)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AbstractApiRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AbstractApiPhoneNumberVerificationSettingsSchema>();
    }
}

/// <summary>
/// Describes the Veriphone provider settings to CrestApps Recipes.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Veriphone)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class VeriphoneRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, VeriphonePhoneNumberVerificationSettingsSchema>();
    }
}

/// <summary>
/// Describes the Twilio Lookup provider settings to CrestApps Recipes.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Twilio)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class TwilioRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, TwilioPhoneNumberVerificationSettingsSchema>();
    }
}
