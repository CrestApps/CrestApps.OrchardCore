using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores which shift reminders were sent.
/// </summary>
internal sealed class ShiftReminderLogStore : DocumentCatalog<ShiftReminderLog, ShiftReminderLogIndex>
{
    public ShiftReminderLogStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var count = await Session.QueryIndex<ShiftReminderLogIndex>(index => index.Key == key, collection: CollectionName).CountAsync(cancellationToken);

        return count > 0;
    }

    public async Task<int> PurgeAsync(DateTime olderThanUtc, int limit, CancellationToken cancellationToken = default)
    {
        var logs = await Session.Query<ShiftReminderLog, ShiftReminderLogIndex>(index => index.SentUtc < olderThanUtc, collection: CollectionName)
            .Take(limit)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var log in logs)
        {
            Session.Delete(log, CollectionName);
            count++;
        }

        return count;
    }
}
