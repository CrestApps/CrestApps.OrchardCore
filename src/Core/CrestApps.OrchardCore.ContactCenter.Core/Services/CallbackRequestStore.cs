using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

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
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<CallbackRequest>> ListDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultBatchSize : maxCount;
        var callbacks = await Session.Query<CallbackRequest, CallbackRequestIndex>(
            index => index.Status == CallbackRequestStatus.Pending &&
                index.ScheduledUtc <= utcNow &&
                (index.LeaseExpiresUtc == null || index.LeaseExpiresUtc <= utcNow),
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.ScheduledUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return callbacks.ToArray();
    }
}
