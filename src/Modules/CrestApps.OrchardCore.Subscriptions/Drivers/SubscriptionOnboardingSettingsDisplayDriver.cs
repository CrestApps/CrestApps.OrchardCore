using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Data;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Displays and updates the settings sold sites are created with: the database they are created on.
/// </summary>
public sealed class SubscriptionOnboardingSettingsDisplayDriver : SiteDisplayDriver<SubscriptionOnboardingSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IEnumerable<DatabaseProvider> _databaseProviders;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionOnboardingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current user.</param>
    /// <param name="authorizationService">The authorization service used to check access to the settings.</param>
    /// <param name="dataProtectionProvider">The provider used to protect the connection string before it is stored.</param>
    /// <param name="databaseProviders">The database providers Orchard Core can create a site on.</param>
    /// <param name="stringLocalizer">The localizer used for validation messages.</param>
    public SubscriptionOnboardingSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IEnumerable<DatabaseProvider> databaseProviders,
        IStringLocalizer<SubscriptionOnboardingSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _databaseProviders = databaseProviders;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the settings group where the subscription onboarding editor is displayed.
    /// </summary>
    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    /// <summary>
    /// Builds the subscription onboarding settings editor when the current user can manage subscription settings.
    /// The stored connection string is never sent back to the browser.
    /// </summary>
    /// <param name="site">The site model that owns the settings.</param>
    /// <param name="settings">The subscription onboarding settings being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the onboarding settings editor, or <see langword="null"/> when access is denied.</returns>
    public override async Task<IDisplayResult> EditAsync(ISite site, SubscriptionOnboardingSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        return Initialize<SubscriptionOnboardingSettingsViewModel>("SubscriptionOnboardingSettings_Edit", model =>
        {
            model.DatabaseProvider = settings.DatabaseProvider;
            model.HasConnectionString = !string.IsNullOrEmpty(settings.ProtectedConnectionString);
            model.Schema = settings.Schema;
            model.DatabaseProviders = _databaseProviders
                .Where(provider => !string.Equals(provider.Value, SubscriptionOnboardingSettings.DefaultDatabaseProvider, StringComparison.OrdinalIgnoreCase))
                .Select(provider => new SelectListItem(provider.Name, provider.Value));
        }).Location("Content:5#Tenant Onboarding:5")
        .OnGroup(SettingsGroupId);
    }

    /// <summary>
    /// Validates and updates the subscription onboarding settings from the submitted editor values. A blank
    /// connection string keeps the stored one as long as the provider is unchanged; a new one is protected before
    /// it is stored.
    /// </summary>
    /// <param name="site">The site model that owns the settings.</param>
    /// <param name="settings">The subscription onboarding settings being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result that renders the updated onboarding settings editor, or <see langword="null"/> when access is denied.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionOnboardingSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return null;
        }

        var model = new SubscriptionOnboardingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var providerValue = string.IsNullOrWhiteSpace(model.DatabaseProvider)
            ? null
            : model.DatabaseProvider.Trim();
        var provider = providerValue is null
            ? null
            : _databaseProviders.FirstOrDefault(candidate => string.Equals(candidate.Value, providerValue, StringComparison.OrdinalIgnoreCase));
        var connectionString = model.ConnectionString?.Trim();

        // A stored connection string belongs to the provider it was entered for.
        var keepsConnectionString = string.IsNullOrEmpty(connectionString) &&
            !string.IsNullOrEmpty(settings.ProtectedConnectionString) &&
            string.Equals(settings.DatabaseProvider, provider?.Value, StringComparison.OrdinalIgnoreCase);

        if (providerValue is not null && provider is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DatabaseProvider), S["The database provider '{0}' isn't available.", providerValue]);
        }
        else if (provider is { HasConnectionString: true } && string.IsNullOrEmpty(connectionString) && !keepsConnectionString)
        {
            // A site cannot be created without a database, and the buyer never sees this screen, so a provider
            // that needs a connection string has to have one before anything is sold.
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionString), S["A connection string is required for the '{0}' provider.", provider.Name]);
        }
        else
        {
            settings.DatabaseProvider = provider?.Value;
            settings.Schema = string.IsNullOrWhiteSpace(model.Schema) ? null : model.Schema.Trim();

            if (provider is not { HasConnectionString: true })
            {
                settings.ProtectedConnectionString = null;
            }
            else if (!string.IsNullOrEmpty(connectionString))
            {
                settings.ProtectedConnectionString = _dataProtectionProvider.ProtectSetting(SubscriptionConstants.ProtectorPurposes.OnboardingConnectionString, connectionString);
            }
        }

        return await EditAsync(site, settings, context);
    }
}
