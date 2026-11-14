using CrestApps.OrchardCore.Locking;
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
    private readonly WorkOrderNotesUpgrade _notesUpgrade;

    public WorkOrdersMigrations(WorkOrderServiceLevelUpgrade upgrade, WorkOrderNotesUpgrade notesUpgrade)
    {
        _upgrade = upgrade;
        _notesUpgrade = notesUpgrade;
    }

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await WorkOrdersIndexSchema.CreateAsync(SchemaBuilder);

        return 4;
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
    public async Task<int> UpdateFrom3Async()
    {
        await _notesUpgrade.RunAsync();

        return 4;
    }
}
