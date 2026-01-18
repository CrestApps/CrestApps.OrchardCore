using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public sealed class OmnichannelActivityStore : DocumentCatalog<OmnichannelActivity, OmnichannelActivityIndex>, IOmnichannelActivityStore
{
    public OmnichannelActivityStore(ISession session)
        : base(session)
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public async Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentContentItemId);

        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                                index.ContactContentItemId == contentContentItemId &&
                                index.Status == ActivityStatus.NotStated &&
                                index.InteractionType == ActivityInteractionType.Manual
                                , collection: OmnichannelConstants.CollectionName)
                            .OrderBy(x => x.ScheduledUtc)
                            .ThenBy(x => x.Id);

        page = Math.Max(page, 0);

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(page * pageSize).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                        index.AssignedToId == userId &&
                        index.Status == ActivityStatus.NotStated &&
                        index.InteractionType == ActivityInteractionType.Manual, collection: OmnichannelConstants.CollectionName)
                    .OrderBy(x => x.ScheduledUtc)
                    .ThenBy(x => x.Id);

        page = Math.Max(page, 0);

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(page * pageSize).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentContentItemId);

        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                                index.ContactContentItemId == contentContentItemId &&
                                index.Status == ActivityStatus.Completed
                                , collection: OmnichannelConstants.CollectionName)
                            .OrderByDescending(x => x.CompletedUtc)
                            .ThenBy(x => x.Id);

        page = Math.Max(page, 0);

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(page * pageSize).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    public async Task<OmnichannelActivity> GetAsync(string channel, string channelEndpointId, string preferredDestination, ActivityInteractionType interactionType)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(channelEndpointId);
        ArgumentException.ThrowIfNullOrEmpty(preferredDestination);

        return await Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
            index.Channel == channel &&
            index.ChannelEndpointId == channelEndpointId &&
            index.PreferredDestination == preferredDestination &&
            index.InteractionType == interactionType, collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();
    }
}
