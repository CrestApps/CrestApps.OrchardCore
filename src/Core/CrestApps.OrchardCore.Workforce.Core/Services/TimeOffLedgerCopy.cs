using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Copies the time-off entries of the Workforce collection, where installs before the shared quantity ledger kept
/// them, to the time-off ledger collection. The copy is additive: the original entries stay, so a node of the earlier
/// release keeps reading them during a rolling upgrade, and an entry already copied is skipped, so the copy can run
/// again to pick up entries such a node wrote meanwhile.
/// </summary>
public static class TimeOffLedgerCopy
{
    /// <summary>
    /// Copies every entry not copied yet, keeping its id.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>How many entries were copied.</returns>
    public static async Task<int> CopyAsync(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var copied = (await session.QueryIndex<QuantityLedgerEntryIndex>(collection: WorkforceConstants.TimeOffLedgerCollectionName).ListAsync())
            .Select(index => index.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        var entries = await session.Query<TimeOffLedgerEntry>(collection: WorkforceConstants.CollectionName).ListAsync();
        var count = 0;

        foreach (var entry in entries.Where(entry => !copied.Contains(entry.ItemId)))
        {
            QuantityLedgerEntry quantity = TimeOffLedgerMapping.ToQuantity(entry);

            await session.SaveAsync(quantity, checkConcurrency: false, collection: WorkforceConstants.TimeOffLedgerCollectionName);
            count++;
        }

        await session.SaveChangesAsync();

        return count;
    }
}
