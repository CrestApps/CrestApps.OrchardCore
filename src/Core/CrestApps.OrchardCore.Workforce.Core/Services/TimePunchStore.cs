using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores punches, and works out which ones count once corrections replace them.
/// </summary>
internal sealed class TimePunchStore : DocumentCatalog<TimePunch, TimePunchIndex>
{
    public TimePunchStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<TimePunch> FindByKeyAsync(string workerId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Session.Query<TimePunch, TimePunchIndex>(index => index.WorkerId == workerId && index.IdempotencyKey == idempotencyKey, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsReplacedAsync(string punchId, CancellationToken cancellationToken = default)
    {
        return await Session.QueryIndex<TimePunchIndex>(index => index.ReplacesPunchId == punchId, collection: CollectionName).CountAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// Returns the punches that count in a range, oldest first: corrections in place of what they replace, and
    /// nothing for removed punches.
    /// </summary>
    public async Task<IReadOnlyList<TimePunch>> ListEffectiveAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var punches = (await Session.Query<TimePunch, TimePunchIndex>(index => index.WorkerId == workerId && index.Utc >= fromUtc && index.Utc < toUtc, collection: CollectionName)
            .ListAsync(cancellationToken)).ToList();

        if (punches.Count == 0)
        {
            return [];
        }

        var ids = punches.Select(punch => punch.ItemId).ToArray();
        var replaced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chunk in ids.Chunk(500))
        {
            var replacing = await Session.QueryIndex<TimePunchIndex>(index => index.ReplacesPunchId.IsIn(chunk), collection: CollectionName).ListAsync(cancellationToken);

            foreach (var row in replacing)
            {
                replaced.Add(row.ReplacesPunchId);
            }
        }

        return punches
            .Where(punch => !punch.Removes && !replaced.Contains(punch.ItemId))
            .OrderBy(punch => punch.Utc)
            .ThenBy(punch => punch.CreatedUtc)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListWorkerIdsSinceAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<TimePunchIndex>(index => index.Utc >= fromUtc, collection: CollectionName).ListAsync(cancellationToken);

        return rows.Select(row => row.WorkerId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
    }
}
