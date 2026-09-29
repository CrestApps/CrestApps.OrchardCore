using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telnyx.Deployments;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;
using OrchardCore.Environment.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Telnyx.Recipes;

/// <summary>
/// Imports the Telnyx SMS provider settings. Members the step carries replace the stored ones; a secret the step does
/// not carry keeps its stored value, and a secret it does carry is protected before it is stored.
/// </summary>
internal sealed class TelnyxSmsSettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsSettingsStep"/> class.
    /// </summary>
    /// <param name="siteService">The site service that stores the settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect supplied secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the options built from the settings.</param>
    public TelnyxSmsSettingsStep(
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier)
        : base(TelnyxDeploymentSteps.SmsSettings)
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
        var settings = site.GetOrCreate<TelnyxSmsSettings>();

        ProtectedSettingsDeploymentSerializer.Populate(settings, data, TelnyxDeploymentSteps.SmsProtectedMembers, _dataProtectionProvider);

        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);

        // The provider options and Orchard Core's SMS provider list are both built from these settings and read through
        // IOptionsMonitor, so signalling them applies the imported values without restarting the tenant.
        _optionsUpdateNotifier
            .RequestUpdate<TelnyxSmsOptions>()
            .RequestUpdate<SmsProviderOptions>();
    }
}
