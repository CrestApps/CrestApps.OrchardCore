using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Migrations;

/// <summary>
/// Adds the content definition metadata for tenant onboarding subscriptions.
/// </summary>
internal sealed class TenantOnboardingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOnboardingMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to update content part definitions.</param>
    public TenantOnboardingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the tenant onboarding content part definition. A new store starts with buyers unable to choose a prefix
    /// or a domain, so the upgrade step that keeps those on for existing stores is skipped.
    /// </summary>
    /// <returns>The migration version after the tenant onboarding definition is created.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("TenantOnboardingPart", part => part
            .Attachable()
            .WithDisplayName("Tenant Onboarding")
            .WithDescription("Provides the key properties for tenant onboarding subscription.")
        );

        return 3;
    }

    /// <summary>
    /// A store that already sold sites with domains of their own, or prefixes other than the site's name, keeps offering
    /// them now that both default to off.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();

            var site = await siteService.LoadSiteSettingsAsync();
            var settings = site.GetOrCreate<SubscriptionOnboardingSettings>();

            // One row per site ever sold through this store, read once at upgrade.
            var jobs = await session
                .Query<TenantProvisioningJob>(collection: SubscriptionConstants.TenantProvisioningCollectionName)
                .ListAsync();

            if (SubscriptionOnboardingUpgrade.Apply(settings, jobs))
            {
                site.Put(settings);
                await siteService.UpdateSiteSettingsAsync(site);
            }
        });

        return 2;
    }

    /// <summary>
    /// A store that could sell sites on a prefix of its own address keeps doing so, now that new stores don't (M10 of the
    /// final security review); the operator reviews the choice under Tenant onboarding.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();
            var settings = site.GetOrCreate<SubscriptionOnboardingSettings>();

            if (SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHost(settings))
            {
                site.Put(settings);
                await siteService.UpdateSiteSettingsAsync(site);
            }
        });

        return 3;
    }
}
