using CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// A YesSql-based implementation of <see cref="ISmsBroadcastStore"/>.
/// </summary>
public sealed class SmsBroadcastStore : DocumentCatalog<SmsBroadcast, SmsBroadcastIndex>, ISmsBroadcastStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SmsBroadcastStore(ISession session)
        : base(session)
    {
        CollectionName = SmsWorkspaceStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SmsBroadcast>> GetByStatusAsync(SmsBroadcastStatus status, CancellationToken cancellationToken = default)
    {
        var value = status.ToString();

        var broadcasts = await Session.Query<SmsBroadcast, SmsBroadcastIndex>(
                index => index.Status == value,
                collection: SmsWorkspaceStorage.CollectionName)
            .ListAsync(cancellationToken);

        return broadcasts.ToArray();
    }
}
