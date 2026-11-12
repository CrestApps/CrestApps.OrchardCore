using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Keeps an existing store working when the onboarding rules arrive. Buyers used to be able to choose any prefix and
/// any domain; new sites now default to neither. A store that already sold sites with domains of their own, or with a
/// prefix other than the site's name, keeps offering them.
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
            if (!settings.AllowCustomDomains && job.Domains is { Length: > 0 })
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
}
