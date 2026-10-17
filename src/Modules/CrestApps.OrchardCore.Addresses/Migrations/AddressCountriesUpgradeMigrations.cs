using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using YesSql;
using DataMigrationRecord = OrchardCore.Data.Migration.Records.DataMigrationRecord;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Earlier 3.0 previews kept the Country content in the Addresses feature itself; it is now the Address Countries
/// feature. A site that ran the old Addresses migration has Country items, perhaps renamed or limited, so the new
/// feature is turned on for it and its country list stays as it was. New sites start without it.
/// </summary>
internal sealed class AddressCountriesUpgradeMigrations : DataMigration
{
    private static readonly string _countriesMigration = typeof(AddressMigrations).FullName;

    private readonly ShellSettings _shellSettings;
    private readonly ISession _session;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public AddressCountriesUpgradeMigrations(ShellSettings shellSettings, ISession session, IShellFeaturesManager shellFeaturesManager)
    {
        _shellSettings = shellSettings;
        _session = session;
        _shellFeaturesManager = shellFeaturesManager;
    }

    public async Task<int> CreateAsync()
    {
        if (_shellSettings.IsInitializing() ||
            !HasRunCountriesMigration(await _session.Query<DataMigrationRecord>().FirstOrDefaultAsync()) ||
            await IsCountriesEnabledAsync(_shellFeaturesManager) ||
            await FindCountriesAsync(_shellFeaturesManager) is null)
        {
            return 1;
        }

        // Turning a feature on reloads the tenant, which can't happen while it starts, so it waits for the end of the
        // request. Until the feature is on, this step returns 0, which leaves it unrecorded, and it runs again the next
        // time the tenant starts.
        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("enable-address-countries", async scope =>
        {
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await IsCountriesEnabledAsync(shellFeaturesManager))
            {
                return;
            }

            if (await FindCountriesAsync(shellFeaturesManager) is { } countries)
            {
                await shellFeaturesManager.EnableFeaturesAsync([countries], force: true);
            }
        });

        return 0;
    }

    internal static bool HasRunCountriesMigration(DataMigrationRecord record)
        => record?.DataMigrations?.Any(migration => migration.DataMigrationClass == _countriesMigration && migration.Version is > 0) == true;

    private static async Task<bool> IsCountriesEnabledAsync(IShellFeaturesManager shellFeaturesManager)
        => (await shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == AddressConstants.Feature.Countries);

    private static async Task<IFeatureInfo> FindCountriesAsync(IShellFeaturesManager shellFeaturesManager)
        => (await shellFeaturesManager.GetAvailableFeaturesAsync()).FirstOrDefault(feature => feature.Id == AddressConstants.Feature.Countries);
}
