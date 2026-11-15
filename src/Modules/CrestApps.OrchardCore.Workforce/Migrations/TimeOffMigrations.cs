using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
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
    /// Moves the time-off ledger to a quantity ledger collection of its own: the new index table now, and the entries
    /// after the migration commits, each with its id, so nothing that refers to an entry changes.
    /// </summary>
    /// <returns>The new version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await QuantityLedgerIndexSchema.CreateAsync(SchemaBuilder, WorkforceConstants.TimeOffLedgerCollectionName);
        await SchemaBuilder.DropMapIndexTableAsync(typeof(TimeOffLedgerEntryIndex), WorkforceConstants.CollectionName);

        // Documents are moved with a session of the shell after the migration: on SQLite a second connection inside
        // the migration would wait forever for the write lock the migration holds.
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<TimeOffMigrations>>();

            try
            {
                await MoveLedgerEntriesAsync(services.GetRequiredService<ISession>());
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The time-off ledger entries could not be moved to the time-off ledger collection. Balances read nothing until they are moved; run the move again by resetting the TimeOffMigrations version to 1.");
            }
        });

        return 2;
    }

    /// <summary>
    /// Moves every time-off entry of the Workforce collection to the time-off ledger collection, keeping its id.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>How many entries were moved.</returns>
    internal static async Task<int> MoveLedgerEntriesAsync(ISession session)
    {
        var entries = await session.Query<TimeOffLedgerEntry>(collection: WorkforceConstants.CollectionName).ListAsync();
        var moved = 0;

        foreach (var entry in entries)
        {
            await session.SaveAsync(TimeOffLedgerMapping.ToQuantity(entry), checkConcurrency: false, collection: WorkforceConstants.TimeOffLedgerCollectionName);
            session.Delete(entry, WorkforceConstants.CollectionName);
            moved++;
        }

        await session.SaveChangesAsync();

        return moved;
    }
}
