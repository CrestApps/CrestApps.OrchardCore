using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ICallbackRequestStore"/>.
/// </summary>
public sealed class CallbackRequestStore : DocumentCatalog<CallbackRequest, CallbackRequestIndex>, ICallbackRequestStore
{
    private const int DefaultBatchSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CallbackRequestStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<CallbackRequest>> GetDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var callbacks = await Session.Query<CallbackRequest, CallbackRequestIndex>(
            index => index.Status == CallbackRequestStatus.Pending &&
                index.ScheduledUtc <= utcNow &&
                (index.LeaseExpiresUtc == null || index.LeaseExpiresUtc <= utcNow),
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.ScheduledUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return callbacks.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<CallbackRequest>> ListOpenForAgentsAsync(
        IEnumerable<string> agentIds,
        DateTime fromUtc,
        DateTime toUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var query = Session.Query<CallbackRequest, CallbackRequestIndex>(
            index => (index.Status == CallbackRequestStatus.Pending ||
                    index.Status == CallbackRequestStatus.Scheduled ||
                    index.Status == CallbackRequestStatus.InProgress) &&
                index.ScheduledUtc >= fromUtc &&
                index.ScheduledUtc < toUtc,
            collection: ContactCenterStorage.CollectionName);

        if (agentIds is null)
        {
            query = query.Where(index => index.AgentId != null);
        }
        else
        {
            var ids = agentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

            if (ids.Length == 0)
            {
                return [];
            }

            query = query.Where(index => index.AgentId.IsIn(ids));
        }

        var callbacks = await query
            .OrderBy(index => index.ScheduledUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return callbacks.ToArray();
    }
}
