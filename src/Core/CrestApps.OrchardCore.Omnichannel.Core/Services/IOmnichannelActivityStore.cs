using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public interface IOmnichannelActivityStore : ICatalog<OmnichannelActivity>
{
    /// <summary>
    /// Pages manually scheduled activities assigned to the specified user.
    /// </summary>
    /// <param name="userId">The assigned user identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="filter">The filter to apply.</param>
    Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter);

    /// <summary>
    /// Pages manually scheduled activities for the specified contact content item.
    /// </summary>
    /// <param name="contentContentItemId">The contact content item identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize);

    /// <summary>
    /// Pages completed manual activities for the specified contact content item.
    /// </summary>
    /// <param name="contentContentItemId">The contact content item identifier.</param>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize);

    /// <summary>
    /// Gets an activity by channel, endpoint, destination, and interaction type.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="channelEndpoint">The channel endpoint identifier or value.</param>
    /// <param name="preferredDestination">The preferred destination to match.</param>
    /// <param name="interactionType">The interaction type to match.</param>
    Task<OmnichannelActivity> GetAsync(string channel, string channelEndpoint, string preferredDestination, ActivityInteractionType interactionType);
}
