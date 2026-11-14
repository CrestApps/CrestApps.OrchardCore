using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using CrestApps.OrchardCore.Staff;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class WorkforceMigrations : DataMigration
{
    private readonly ActivityCodeSeeder _activityCodeSeeder;
    private readonly IStore _store;
    private readonly ISession _session;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public WorkforceMigrations(
        ActivityCodeSeeder activityCodeSeeder,
        IStore store,
        ISession session,
        IShellFeaturesManager shellFeaturesManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _dataProtectionProvider = dataProtectionProvider;
        _activityCodeSeeder = activityCodeSeeder;
        _store = store;
        _session = session;
    }

    public async Task<int> CreateAsync()
    {
        await WorkforceIndexSchema.CreateAsync(SchemaBuilder);
        await _activityCodeSeeder.SeedAsync();

        // Teams live in the Teams module and workers are staff members from the start, so there is nothing to move.
        return 3;
    }

    // Teams were kept in the Workforce collection before they had their own module.
    public async Task<int> UpdateFrom1Async()
    {
        await WorkforceTeamsMove.MoveAsync(SchemaBuilder, _store, _session);

        return 2;
    }

    // Workers became staff members, kept by the Staff module with the same ids. A site that ran Workforce before Staff
    // existed gets Staff turned on first; the move runs at the next start, once the Staff tables are there.
    public async Task<int> UpdateFrom2Async()
    {
        if (!(await _shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == StaffConstants.Features.Staff))
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("enable-staff", static async scope =>
            {
                var features = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

                if ((await features.GetEnabledFeaturesAsync()).Any(feature => feature.Id == StaffConstants.Features.Staff))
                {
                    return;
                }

                if ((await features.GetAvailableFeaturesAsync()).FirstOrDefault(feature => feature.Id == StaffConstants.Features.Staff) is { } staff)
                {
                    // Without force, the feature is refused when any feature in its dependency chain is still off.
                    await features.EnableFeaturesAsync([staff], force: true);
                }
            });

            return 2;
        }

        // Working staff with an account are put in the Staff role by the hourly staff upkeep, or when they are next saved.
        // Feed secrets are hashed as the workers move: the portal's backfill ran on the start that turned Staff on.
        await WorkforceWorkersMove.MoveAsync(SchemaBuilder, _store, _session, _dataProtectionProvider);

        return 3;
    }
}
