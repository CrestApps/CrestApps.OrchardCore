using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Keeps an existing store working when the onboarding rules arrive. Buyers used to be able to choose any prefix and
/// any domain; new sites now default to neither. A store that already sold sites with domains of their own, or with a
/// prefix other than the site's name, keeps offering them; domains only once the store lists its own host names.
/// </summary>
public static class SubscriptionOnboardingUpgrade
{
    /// <summary>
    /// Turns on what the store's completed sites show it already offered.
    /// </summary>
    /// <param name="settings">The onboarding settings to change.</param>
    /// <param name="jobs">The store's provisioning jobs.</param>
    /// <returns><see langword="true"/> when the settings changed.</returns>
    public static bool Apply(SubscriptionOnboardingSettings settings, IEnumerable<TenantProvisioningJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var changed = false;

        foreach (var job in (jobs ?? []).Where(job => job.Status == TenantProvisioningStatus.Succeeded))
        {
            // Custom domains need the operator's own host names listed first, as the settings editor requires: the default
            // tenant normally has none, so a buyer could otherwise take an alias of the store. They stay off until the list
            // is filled in.
            if (!settings.AllowCustomDomains && job.Domains is { Length: > 0 } && settings.HasReservedHosts())
            {
                settings.AllowCustomDomains = true;
                changed = true;
            }

            if (!settings.AllowCustomPrefix &&
                !string.IsNullOrEmpty(job.Prefix) &&
                !string.Equals(job.Prefix.Trim('/'), job.TenantName, StringComparison.OrdinalIgnoreCase))
            {
                settings.AllowCustomPrefix = true;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Keeps an existing store selling sites on a prefix of its own address where its completed sites show it did, now
    /// that new stores don't: a site that succeeded on a prefix with no domain of its own. A store that never sold one keeps
    /// the setting off, and the onboarding settings and the Site provisioning page tell the operator how to sell sites.
    /// </summary>
    /// <param name="settings">The onboarding settings to change.</param>
    /// <param name="jobs">The store's provisioning jobs.</param>
    /// <returns><see langword="true"/> when the settings changed.</returns>
    public static bool KeepPrefixSitesOnStoreHost(SubscriptionOnboardingSettings settings, IEnumerable<TenantProvisioningJob> jobs)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.AllowPrefixOnStoreHost ||
            (!string.IsNullOrWhiteSpace(settings.BaseDomain) && !settings.AllowCustomDomains) ||
            !(jobs ?? []).Any(SoldOnStoreHost))
        {
            return false;
        }

        settings.AllowPrefixOnStoreHost = true;

        return true;
    }

    /// <summary>
    /// Runs <see cref="KeepPrefixSitesOnStoreHost"/> against the store: reads its provisioning jobs once and saves the
    /// onboarding settings when they change. The upgrade step runs it once the shell is ready.
    /// </summary>
    /// <param name="session">The session the provisioning jobs are read with.</param>
    /// <param name="siteService">The site service holding the onboarding settings.</param>
    public static async Task KeepPrefixSitesOnStoreHostAsync(ISession session, ISiteService siteService)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(siteService);

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<SubscriptionOnboardingSettings>();

        // Only completed sites count, read once at upgrade.
        var succeeded = await session
            .Query<TenantProvisioningJob, TenantProvisioningJobIndex>(
                index => index.Status == TenantProvisioningStatus.Succeeded,
                collection: SubscriptionConstants.TenantProvisioningCollectionName)
            .ListAsync();

        if (KeepPrefixSitesOnStoreHost(settings, succeeded))
        {
            site.Put(settings);
            await siteService.UpdateSiteSettingsAsync(site);
        }
    }

    // A site on a prefix with no host of its own answers on the store's address.
    private static bool SoldOnStoreHost(TenantProvisioningJob job)
        => job.Status == TenantProvisioningStatus.Succeeded &&
            !string.IsNullOrEmpty(job.Prefix?.Trim('/')) &&
            job.Domains is not { Length: > 0 };
}
