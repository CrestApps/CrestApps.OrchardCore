using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk.Deployments;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Asterisk.Recipes;

/// <summary>
/// Imports the Asterisk provider settings. Members the step carries replace the stored ones; a secret the step does not
/// carry keeps its stored value, and a secret it does carry is protected before it is stored.
/// </summary>
/// <remarks>
/// The settings screen refuses to abandon the tenant's live ARI identity (disabling the provider, or changing its base
/// URL or Stasis application) while Contact Center calls are still bound to it, because another tenant could then claim
/// the identity and observe those calls. An import is held to the same rule.
/// </remarks>
internal sealed class AsteriskSettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly IAsteriskChannelTenantBindingStore _channelTenantBindingStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsStep"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect supplied secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the options built from the settings.</param>
    /// <param name="channelTenantBindingStores">The optional store of live Contact Center call bindings.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AsteriskSettingsStep(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IEnumerable<IAsteriskChannelTenantBindingStore> channelTenantBindingStores,
        IStringLocalizer<AsteriskSettingsStep> stringLocalizer)
        : base(AsteriskDeploymentSteps.Settings)
    {
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _channelTenantBindingStore = channelTenantBindingStores.FirstOrDefault();
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Settings"] is not JsonObject data)
        {
            return;
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<AsteriskSettings>();

        var previousIsEnabled = settings.IsEnabled;
        var previousBaseUrl = settings.BaseUrl;
        var previousApplicationName = settings.ApplicationName;

        ProtectedSettingsDeploymentSerializer.Populate(settings, data, AsteriskDeploymentSteps.ProtectedMembers, _dataProtectionProvider);

        settings.BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl
            : AsteriskSettingsUtilities.NormalizeBaseUrl(settings.BaseUrl);

        if (previousIsEnabled &&
            IsAriIdentityAbandoned(previousBaseUrl, previousApplicationName, settings) &&
            _channelTenantBindingStore is not null &&
            await _channelTenantBindingStore.HasAnyAsync())
        {
            context.Errors.Add(S["The Asterisk settings were not imported because this Asterisk endpoint still owns one or more live call channels. End or reconcile those calls before you disable the provider or change its ARI URL or Stasis application name."]);

            return;
        }

        site.Put(settings);

        // Enabling the provider on a tenant that has no default provider makes it the default, as saving it does.
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = AsteriskConstants.ProviderTechnicalName;
            site.Put(telephonySettings);
        }

        await _siteService.UpdateSiteSettingsAsync(site);

        // Every Asterisk service reads the settings live; only the shared provider map is a cached projection.
        _optionsUpdateNotifier.RequestUpdate<TelephonyProviderOptions>();
    }

    private static bool IsAriIdentityAbandoned(string previousBaseUrl, string previousApplicationName, AsteriskSettings settings)
    {
        if (!settings.IsEnabled)
        {
            return true;
        }

        var previousNormalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(previousBaseUrl);
        var currentNormalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(settings.BaseUrl);

        if (!string.Equals(previousNormalizedBaseUrl, currentNormalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.Equals(previousApplicationName?.Trim(), settings.ApplicationName?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
