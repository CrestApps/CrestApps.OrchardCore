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
    public async Task<IReadOnlyCollection<CallbackRequest>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var callbacks = await Session.Query<CallbackRequest, CallbackRequestIndex>(
            index => index.Status == CallbackRequestStatus.Pending && index.ScheduledUtc <= utcNow,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.ScheduledUtc)
            .ListAsync(cancellationToken);

        return callbacks.ToArray();
    }
}
