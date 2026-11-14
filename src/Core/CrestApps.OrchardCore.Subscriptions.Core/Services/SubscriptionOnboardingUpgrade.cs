using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;

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
    /// Keeps an existing store selling sites on a prefix of its own address where it could before, now that new stores
    /// don't: without a base domain, or with buyers' own domains (a buyer who names none gets a prefix). Upgrading leaves
    /// the choice made and visible in the settings, and the upgrade notes ask the operator to review it.
    /// </summary>
    /// <param name="settings">The onboarding settings to change.</param>
    /// <returns><see langword="true"/> when the settings changed.</returns>
    public static bool KeepPrefixSitesOnStoreHost(SubscriptionOnboardingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.AllowPrefixOnStoreHost || (!string.IsNullOrWhiteSpace(settings.BaseDomain) && !settings.AllowCustomDomains))
        {
            return false;
        }

        settings.AllowPrefixOnStoreHost = true;

        return true;
    }
}
