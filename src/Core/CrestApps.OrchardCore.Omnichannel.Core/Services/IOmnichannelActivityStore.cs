using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the contract for omnichannel activity store.
/// </summary>
public interface IOmnichannelActivityStore : ICatalog<OmnichannelActivity>
{
    /// <summary>
    /// Pages manually scheduled activities assigned to the specified user.
    /// </summary>
    /// <param name="userId">The assigned user identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="filter">The filter to apply.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages manually scheduled activities for the specified contact content item.
    /// </summary>
    /// <param name="contentContentItemId">The contact content item identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages completed manual activities for the specified contact content item.
    /// </summary>
    /// <param name="contentContentItemId">The contact content item identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages bulk-manageable activities (NotStarted status with Manual interaction type) using the specified filter.
    /// </summary>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="filter">The bulk manage filter criteria.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<OmnichannelActivity>> PageBulkManageableAsync(int page, int pageSize, BulkManageActivityFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all bulk-manageable activities (NotStarted status with Manual interaction type) using the specified filter.
    /// </summary>
    /// <param name="filter">The bulk manage filter criteria.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<OmnichannelActivity>> ListBulkManageableAsync(BulkManageActivityFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an activity by channel, endpoint, destination, and interaction type.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="channelEndpoint">The channel endpoint identifier or value.</param>
    /// <param name="preferredDestination">The preferred destination to match.</param>
    /// <param name="interactionType">The interaction type to match.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<OmnichannelActivity> GetAsync(string channel, string channelEndpoint, string preferredDestination, ActivityInteractionType interactionType, CancellationToken cancellationToken = default);
}
