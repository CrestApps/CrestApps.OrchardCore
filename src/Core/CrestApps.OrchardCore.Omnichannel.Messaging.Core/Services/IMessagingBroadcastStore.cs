using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The persistence contract for <see cref="MessagingBroadcast"/>.
/// </summary>
public interface IMessagingBroadcastStore : ICatalog<MessagingBroadcast>
{
    /// <summary>
    /// Lists the broadcasts in the specified status, oldest first.
    /// </summary>
    /// <param name="status">The status to match.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching broadcasts.</returns>
    Task<IReadOnlyCollection<MessagingBroadcast>> GetByStatusAsync(MessagingBroadcastStatus status, CancellationToken cancellationToken = default);
}
