using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Locking;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking.Distributed;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the work order index tables.
/// </summary>
internal sealed class WorkOrdersMigrations : DataMigration
{
    private readonly WorkOrderServiceLevelUpgrade _upgrade;

    public WorkOrdersMigrations(WorkOrderServiceLevelUpgrade upgrade)
    {
        _upgrade = upgrade;
    }

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await WorkOrdersIndexSchema.CreateAsync(SchemaBuilder);

        return 5;
    }

    /// <summary>
    /// Adds the CRM account column, which replaced the Companies module's company.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<WorkOrderIndex>(table =>
        {
            table.AddColumn<string>("AccountId", column => column.Nullable().WithLength(64));
            table.CreateIndex("IDX_WorkOrderIndex_Account", "AccountId");
        }, collection: WorkOrdersConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Moves the policies and clocks Work Orders kept to the shared Service Levels block, which this feature depends on,
    /// so its tables exist by now.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await _upgrade.CopyPoliciesAsync();

        // The timers read the schedules just copied, which other scopes see once this migration commits.
        ShellScope.AddDeferredTask(StartOpenWorkOrdersAsync);

        return 3;
    }

    // One node gives the open work orders their timers; a node that finds the lock taken leaves it to the one holding it.
    private static async Task StartOpenWorkOrdersAsync(ShellScope scope)
    {
        var services = scope.ServiceProvider;
        var (locker, locked) = await services.GetRequiredService<IDistributedLock>()
            .TryAcquireLockWithoutWaitingAsync("WorkOrders:ServiceLevelUpgrade", TimeSpan.FromHours(1));

        if (!locked)
        {
            return;
        }

        await using (locker)
        {
            var started = await services.GetRequiredService<WorkOrderServiceLevelUpgrade>().StartOpenWorkOrdersAsync();

            var logger = services.GetRequiredService<ILogger<WorkOrdersMigrations>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Gave {Count} open work orders their service level timers.", started);
            }
        }
    }

    /// <summary>
    /// Moves the notes stored on work orders to their timelines, which this feature depends on.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom3()
    {
        // The move runs once this step has committed, a batch at a time, so the Timeline tables created in the same
        // activation are kept whatever happens.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());

        return 4;
    }

    /// <summary>
    /// Keeps the service level policies page for the roles that managed it as part of the work order settings: the
    /// policies moved to the shared Service Levels block, which has its own permission.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom4()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(scope => RolePermissionMigrations.GrantAsync(
            scope.ServiceProvider,
            new Dictionary<string, string[]>
            {
                [WorkOrdersPermissions.ManageWorkOrderSettings.Name] = [ServiceLevelsPermissions.ManageServiceLevelPolicies.Name],
            },
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<WorkOrdersMigrations>()));

        return 5;
    }
}
