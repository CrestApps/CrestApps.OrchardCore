using CrestApps.OrchardCore.Accounts;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Enables the Accounts feature on a site that had the CRM on before accounts had a feature of their own.
/// </summary>
/// <remarks>
/// The CRM depends on Accounts, so Orchard Core loads it, but a feature that only became a dependency is not recorded
/// as enabled. Recording it keeps it on, with its menu, search and picker, however the site's features change later.
/// The site's account type and its accounts were created by the CRM under the same names and are left as they are.
/// </remarks>
internal sealed class CrmAccountsFeatureMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmAccountsFeatureMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    public CrmAccountsFeatureMigrations(ShellSettings shellSettings, IShellFeaturesManager shellFeaturesManager)
    {
        _shellSettings = shellSettings;
        _shellFeaturesManager = shellFeaturesManager;
    }

    /// <summary>
    /// Enables the Accounts feature when it is not recorded as enabled.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        if (_shellSettings.IsInitializing() || await IsAccountsEnabledAsync(_shellFeaturesManager))
        {
            return 1;
        }

        // Migrations that run while a tenant starts run no deferred tasks, so the feature is enabled after the request
        // ends instead. Returning 0 leaves the migration unrecorded, so it runs again at the next start until the
        // feature is on.
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("enable-accounts", static async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await IsAccountsEnabledAsync(shellFeaturesManager))
            {
                return;
            }

            var feature = (await shellFeaturesManager.GetAvailableFeaturesAsync())
                .FirstOrDefault(feature => feature.Id == AccountsConstants.Feature);

            if (feature is not null)
            {
                // Without force, the feature is refused when any feature in its dependency chain is still off.
                await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
            }
        });

        return 0;
    }

    private static async Task<bool> IsAccountsEnabledAsync(IShellFeaturesManager shellFeaturesManager)
        => (await shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == AccountsConstants.Feature);
}
