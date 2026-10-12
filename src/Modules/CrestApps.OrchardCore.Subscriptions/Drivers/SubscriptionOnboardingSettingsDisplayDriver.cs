using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
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
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionOnboardingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer used for option labels and validation messages.</param>
    public SubscriptionOnboardingSettingsDisplayDriver(IStringLocalizer<SubscriptionOnboardingSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the settings group where the subscription onboarding editor is displayed.
    /// </summary>
    protected override string SettingsGroupId
        => SubscriptionSettingsDisplayDriver.GroupId;

    /// <summary>
    /// Builds the subscription onboarding settings editor.
    /// </summary>
    /// <param name="model">The site model that owns the settings.</param>
    /// <param name="settings">The subscription onboarding settings being edited.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result that renders the onboarding settings editor.</returns>
    public override IDisplayResult Edit(ISite model, SubscriptionOnboardingSettings settings, BuildEditorContext context)
    {
        return Initialize<SubscriptionOnboardingSettingsViewModel>("SubscriptionOnboardingSettings_Edit", model =>
        {
            model.DatabaseProvider = settings.DatabaseProvider;
            model.ConnectionString = settings.ConnectionString;
            model.Schema = settings.Schema;
            model.DatabaseProviders = _providers.Select(provider => new SelectListItem(provider.Name, provider.Value));
        }).Location("Content:5#Tenant Onboarding:5")
        .OnGroup(SettingsGroupId);
    }

    /// <summary>
    /// Validates and updates the subscription onboarding settings from the submitted editor values.
    /// </summary>
    /// <param name="site">The site model that owns the settings.</param>
    /// <param name="settings">The subscription onboarding settings being updated.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result that renders the updated onboarding settings editor.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SubscriptionOnboardingSettings settings, UpdateEditorContext context)
    {
        var model = new SubscriptionOnboardingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var match = Array.FindIndex(_providers, candidate =>
            string.Equals(candidate.Value, model.DatabaseProvider, StringComparison.OrdinalIgnoreCase));

        // A site cannot be created without a database, and the buyer never sees this screen, so a provider
        // that needs a connection string has to have one before anything is sold.
        if (match >= 0 && _providers[match].NeedsConnectionString && string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionString), S["A connection string is required for the '{0}' provider.", _providers[match].Name]);
        }
        else
        {
            settings.DatabaseProvider = model.DatabaseProvider;
            settings.ConnectionString = model.ConnectionString;
            settings.Schema = model.Schema;
        }

        return Edit(site, settings, context);
    }

    // The providers a site can be created on. Sqlite is first because it is the one that needs nothing
    // configured, and so the one a site can be sold on before an operator has set anything up.
    private static readonly (string Value, string Name, bool NeedsConnectionString)[] _providers =
    [
        ("Sqlite", "Sqlite", false),
        ("SqlConnection", "SQL Server", true),
        ("Postgres", "PostgreSQL", true),
        ("MySql", "MySQL", true),
    ];
}
