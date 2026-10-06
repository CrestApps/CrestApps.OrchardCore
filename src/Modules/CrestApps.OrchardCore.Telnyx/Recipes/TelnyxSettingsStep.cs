using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Deployments;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Recipes;

/// <summary>
/// Imports the Telnyx voice provider settings. Members the step carries replace the stored ones; a secret the step does
/// not carry keeps its stored value, and a secret it does carry is protected before it is stored.
/// </summary>
internal sealed class TelnyxSettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSettingsStep"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect supplied secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the options built from the settings.</param>
    public TelnyxSettingsStep(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier)
        : base(TelnyxDeploymentSteps.Settings)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Settings"] is not JsonObject data)
        {
            return;
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<TelnyxSettings>();

        ProtectedSettingsDeploymentSerializer.Populate(settings, data, TelnyxDeploymentSteps.VoiceProtectedMembers, _dataProtectionProvider);

        // The settings screen applies the same normalization, so an imported value reads exactly as a saved one would.
        settings.WebRtcRegion = TelnyxSignalingRegions.Normalize(settings.WebRtcRegion);
        settings.NoiseSuppressionEngine = TelnyxNoiseSuppressionService.NormalizeEngine(settings.NoiseSuppressionEngine);

        if (settings.CredentialLifetimeMinutes <= 0)
        {
            settings.CredentialLifetimeMinutes = 180;
        }

        site.Put(settings);

        // Enabling the provider on a tenant that has no default provider makes it the default, as saving it does.
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = TelnyxConstants.ProviderTechnicalName;
            site.Put(telephonySettings);
        }

        await _siteService.UpdateSiteSettingsAsync(site);

        _optionsUpdateNotifier
            .RequestUpdate<TelnyxOptions>()
            .RequestUpdate<TelephonyProviderOptions>();
    }
}
