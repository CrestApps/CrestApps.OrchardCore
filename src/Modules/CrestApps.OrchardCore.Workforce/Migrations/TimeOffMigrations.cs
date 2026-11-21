using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Workforce.Migrations;

internal sealed class TimeOffMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TimeOffIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Moves the time-off ledger to a quantity ledger collection of its own: the new index table now. The entries are
    /// copied by <see cref="TimeOffLedgerCopy"/> at each start of the tenant, after the migrations, with their ids, so
    /// nothing that refers to an entry changes; accrual waits until the copy recorded that it read to the end. The old
    /// entries and the old index table stay until a later release, so a node of the earlier release keeps working during
    /// a rolling upgrade.
    /// </summary>
    /// <returns>The new version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await QuantityLedgerIndexSchema.CreateAsync(SchemaBuilder, WorkforceConstants.TimeOffLedgerCollectionName);

        return 2;
    }
}
