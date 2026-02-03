using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public interface IOmnichannelActivityStore : ICatalog<OmnichannelActivity>
{
    Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter);

    Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize);

    Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize);

    Task<OmnichannelActivity> GetAsync(string channel, string channelEndpoint, string preferredDestination, ActivityInteractionType interactionType);
}
