using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class TimeOffMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TimeOffIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Moves the time-off ledger to a quantity ledger collection of its own: the new index table now, and a copy of the
    /// entries after the migration commits, each with its id, so nothing that refers to an entry changes. The old
    /// entries and the old index table stay until a later release, so a node of the earlier release keeps working
    /// during a rolling upgrade.
    /// </summary>
    /// <returns>The new version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await QuantityLedgerIndexSchema.CreateAsync(SchemaBuilder, WorkforceConstants.TimeOffLedgerCollectionName);

        // Documents are copied with a session of the shell after the migration: on SQLite a second connection inside
        // the migration would wait forever for the write lock the migration holds.
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<TimeOffMigrations>>();

            try
            {
                await TimeOffLedgerCopy.CopyAsync(services.GetRequiredService<ISession>());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The time-off ledger entries could not be copied to the time-off ledger collection. Balances read nothing until they are copied; run the copy again by resetting the TimeOffMigrations version to 1.");
            }
        });

        return 2;
    }
}
