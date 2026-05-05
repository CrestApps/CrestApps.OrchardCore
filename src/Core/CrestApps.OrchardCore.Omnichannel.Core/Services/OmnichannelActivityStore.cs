using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IOmnichannelActivityStore"/> for persisting and querying omnichannel activities.
/// </summary>
public sealed class OmnichannelActivityStore : DocumentCatalog<OmnichannelActivity, OmnichannelActivityIndex>, IOmnichannelActivityStore
{
    private readonly IEnumerable<IListOmnichannelActivityFilterHandler> _handlers;
    private readonly IEnumerable<IBulkManageActivityFilterHandler> _bulkManageHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="handlers">The filter handlers applied when listing activities.</param>
    /// <param name="bulkManageHandlers">The filter handlers applied when bulk managing activities.</param>
    public OmnichannelActivityStore(
        ISession session,
        IEnumerable<IListOmnichannelActivityFilterHandler> handlers,
        IEnumerable<IBulkManageActivityFilterHandler> bulkManageHandlers)
        : base(session)
    {
        CollectionName = OmnichannelConstants.CollectionName;
        _handlers = handlers;
        _bulkManageHandlers = bulkManageHandlers;
    }

    /// <inheritdoc/>
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

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(filter);

        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.AssignedToId == userId &&
            index.Status == ActivityStatus.NotStated &&
            index.InteractionType == ActivityInteractionType.Manual
            , collection: OmnichannelConstants.CollectionName);

        var context = new ListOmnichannelActivityFilterContext(filter, query);

        foreach (var handler in _handlers)
        {
            await handler.FilteringAsync(context);
        }

        query = context.Query.With<OmnichannelActivityIndex>()
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenBy(x => x.Id);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageBulkManageableAsync(int page, int pageSize, BulkManageActivityFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var filteredQuery = await BuildBulkManageableQueryAsync(filter);
        var orderedQuery = filteredQuery.With<OmnichannelActivityIndex>()
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenBy(x => x.Id);
        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<OmnichannelActivity>()
        {
            Count = await filteredQuery.CountAsync(),
            Entries = (await orderedQuery.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OmnichannelActivity>> ListBulkManageableAsync(BulkManageActivityFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var filteredQuery = await BuildBulkManageableQueryAsync(filter);

        var activities = await filteredQuery
            .With<OmnichannelActivityIndex>()
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenBy(x => x.Id)
            .ListAsync();

        return activities.ToArray();
    }

    private async Task<IQuery<OmnichannelActivity>> BuildBulkManageableQueryAsync(BulkManageActivityFilter filter)
    {
        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(collection: OmnichannelConstants.CollectionName)
            .Where(index => index.Status == ActivityStatus.NotStated && index.InteractionType == ActivityInteractionType.Manual);

        var context = new BulkManageActivityFilterContext(filter, query);

        foreach (var handler in _bulkManageHandlers)
        {
            await handler.FilteringAsync(context);
        }

        var filteredQuery = context.Query;

        if (filter.ContactIsPublished.HasValue)
        {
            var contactContentItemIds = (await filteredQuery.ListAsync())
                .Select(activity => activity.ContactContentItemId)
                .Where(contentItemId => !string.IsNullOrEmpty(contentItemId))
                .Distinct()
                .ToArray();

            if (contactContentItemIds.Length == 0)
            {
                return Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.DocumentId == -1, collection: OmnichannelConstants.CollectionName);
            }

            var contactQuery = Session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId.IsIn(contactContentItemIds));

            contactQuery = filter.ContactIsPublished.Value
                ? contactQuery.Where(index => index.Published)
                : contactQuery.Where(index => index.Latest && !index.Published);

            var filteredContactContentItemIds = (await contactQuery.ListAsync())
                .Select(contentItem => contentItem.ContentItemId)
                .Distinct()
                .ToArray();

            if (filteredContactContentItemIds.Length == 0)
            {
                return Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.DocumentId == -1, collection: OmnichannelConstants.CollectionName);
            }

            filteredQuery = filteredQuery.With<OmnichannelActivityIndex>(index => index.ContactContentItemId.IsIn(filteredContactContentItemIds));
        }

        return filteredQuery;
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentContentItemId);

        var query = Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.ContactContentItemId == contentContentItemId &&
            index.Status == ActivityStatus.Completed
            , collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(x => x.CompletedUtc)
            .ThenBy(x => x.Id);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync()).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<OmnichannelActivity> GetAsync(string channel, string channelEndpointId, string preferredDestination, ActivityInteractionType interactionType)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentException.ThrowIfNullOrEmpty(channelEndpointId);
        ArgumentException.ThrowIfNullOrEmpty(preferredDestination);

        return await Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index => index.Channel == channel &&
            index.ChannelEndpointId == channelEndpointId &&
            index.PreferredDestination == preferredDestination &&
            index.InteractionType == interactionType, collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();
    }
}
