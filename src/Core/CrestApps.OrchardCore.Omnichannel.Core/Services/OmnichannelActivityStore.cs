using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Dapper;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IOmnichannelActivityStore"/> for persisting and querying omnichannel activities.
/// </summary>
public sealed class OmnichannelActivityStore : DocumentCatalog<OmnichannelActivity, OmnichannelActivityIndex>, IOmnichannelActivityStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    private const string ActivityAlias = "a";

    private readonly IEnumerable<IListOmnichannelActivityFilterHandler> _handlers;
    private readonly IEnumerable<IBulkManageActivityFilterHandler> _bulkManageHandlers;
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="handlers">The filter handlers applied when listing activities.</param>
    /// <param name="bulkManageHandlers">The filter handlers applied when bulk managing activities.</param>
    /// <param name="store">The YesSql store for SQL configuration.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    public OmnichannelActivityStore(
        ISession session,
        IEnumerable<IListOmnichannelActivityFilterHandler> handlers,
        IEnumerable<IBulkManageActivityFilterHandler> bulkManageHandlers,
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor)
        : base(session)
    {
        CollectionName = OmnichannelConstants.CollectionName;
        _handlers = handlers;
        _bulkManageHandlers = bulkManageHandlers;
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageContactManualScheduledAsync(string contentContentItemId, int page, int pageSize, CancellationToken cancellationToken = default)
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
            Count = await query.CountAsync(cancellationToken),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageManualScheduledAsync(string userId, int page, int pageSize, ListOmnichannelActivityFilter filter, CancellationToken cancellationToken = default)
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
            await handler.FilteringAsync(context, cancellationToken);
        }

        query = context.Query.With<OmnichannelActivityIndex>()
            .OrderByDescending(x => x.ScheduledUtc)
            .ThenBy(x => x.Id);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<OmnichannelActivity>()
        {
            Count = await query.CountAsync(cancellationToken),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageBulkManageableAsync(int page, int pageSize, BulkManageActivityFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var context = await BuildBulkManageableContextAsync(filter, cancellationToken);

        // Clone the builder for counting (without ORDER BY / pagination).
        var countBuilder = context.SqlBuilder.Clone();
        countBuilder.ClearOrder();
        countBuilder.Selector("COUNT(*)");

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countBuilder.ToSqlString(), context.Parameters, cancellationToken: cancellationToken));

        if (filter.Limit.HasValue && filter.Limit.Value > 0)
        {
            totalCount = Math.Min(totalCount, filter.Limit.Value);
        }

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        var take = filter.Limit.HasValue && filter.Limit.Value > 0
            ? Math.Min(pageSize, Math.Max(0, filter.Limit.Value - skip))
            : pageSize;

        if (take <= 0)
        {
            return new PageResult<OmnichannelActivity>()
            {
                Count = totalCount,
                Entries = [],
            };
        }

        // Add pagination to the main query.
        context.SqlBuilder.Skip(skip.ToString());
        context.SqlBuilder.Take(take.ToString());

        var documentIds = (await connection.QueryAsync<long>(
            new CommandDefinition(context.SqlBuilder.ToSqlString(), context.Parameters, cancellationToken: cancellationToken)))
            .ToArray();

        var entries = documentIds.Length > 0
            ? (await Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.DocumentId.IsIn(documentIds), collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken)).ToArray()
            : [];

        return new PageResult<OmnichannelActivity>()
        {
            Count = totalCount,
            Entries = entries,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OmnichannelActivity>> ListBulkManageableAsync(BulkManageActivityFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var context = await BuildBulkManageableContextAsync(filter, cancellationToken);

        if (filter.Limit.HasValue && filter.Limit.Value > 0)
        {
            context.SqlBuilder.Take(filter.Limit.Value.ToString());
        }

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var documentIds = (await connection.QueryAsync<long>(
            new CommandDefinition(context.SqlBuilder.ToSqlString(), context.Parameters, cancellationToken: cancellationToken)))
            .ToArray();

        if (documentIds.Length == 0)
        {
            return [];
        }

        var activities = await Session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
            index => index.DocumentId.IsIn(documentIds), collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return activities.ToArray();
    }

    private async Task<BulkManageActivityFilterContext> BuildBulkManageableContextAsync(BulkManageActivityFilter filter, CancellationToken cancellationToken)
    {
        var tableNameConvention = _store.Configuration.TableNameConvention;
        var dialect = _store.Configuration.SqlDialect;
        var tablePrefix = _store.Configuration.TablePrefix;
        var schema = _store.Configuration.Schema;
        var activityTableName = tableNameConvention.GetIndexTable(typeof(OmnichannelActivityIndex), OmnichannelConstants.CollectionName);

        var sqlBuilder = new SqlBuilder(tablePrefix, dialect);
        sqlBuilder.Select();
        sqlBuilder.Selector($"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.DocumentId))}");
        sqlBuilder.Table(activityTableName, ActivityAlias, schema);

        // Base conditions: only editable inventory, excluding completed, purged, and in-flight work.
        var statusCol = $"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Status))}";
        sqlBuilder.WhereAnd($"{statusCol} IN ({(int)ActivityStatus.NotStated}, {(int)ActivityStatus.Scheduled}, {(int)ActivityStatus.Pending}, {(int)ActivityStatus.AwaitingAgentResponse}, {(int)ActivityStatus.Failed}, {(int)ActivityStatus.Cancelled})");

        // Default ordering.
        var scheduledCol = $"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ScheduledUtc))}";
        var idCol = $"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Id))}";
        sqlBuilder.OrderByDescending(scheduledCol);
        sqlBuilder.ThenOrderBy(idCol);

        var context = new BulkManageActivityFilterContext(filter, sqlBuilder, dialect, tablePrefix, tableNameConvention, schema, ActivityAlias);

        foreach (var handler in _bulkManageHandlers)
        {
            await handler.FilteringAsync(context, cancellationToken);
        }

        // Handle ContactIsPublished filter if needed (requires cross-collection check with ContentItemIndex).
        if (filter.ContactIsPublished.HasValue)
        {
            await ApplyContactPublishedFilterAsync(context, filter.ContactIsPublished.Value);
        }

        return context;
    }

    private static Task ApplyContactPublishedFilterAsync(BulkManageActivityFilterContext context, bool published)
    {
        var dialect = context.Dialect;
        var contactAlias = "ci";
        var contentItemTable = context.TableNameConvention.GetIndexTable(typeof(ContentItemIndex));
        var activityContactCol = nameof(OmnichannelActivityIndex.ContactContentItemId);
        var contentItemIdCol = nameof(ContentItemIndex.ContentItemId);

        context.SqlBuilder.Join(
            JoinType.Inner,
            contentItemTable,
            contactAlias,
            contentItemIdCol,
            context.ActivityTableAlias,
            activityContactCol,
            context.Schema,
            contactAlias,
            context.ActivityTableAlias);

        var publishedCol = $"{dialect.QuoteForAliasName(contactAlias)}.{dialect.QuoteForColumnName(nameof(ContentItemIndex.Published))}";
        var latestCol = $"{dialect.QuoteForAliasName(contactAlias)}.{dialect.QuoteForColumnName(nameof(ContentItemIndex.Latest))}";

        if (published)
        {
            context.SqlBuilder.WhereAnd($"{publishedCol} = 1");
        }
        else
        {
            context.SqlBuilder.WhereAnd($"{latestCol} = 1 AND {publishedCol} = 0");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<PageResult<OmnichannelActivity>> PageContactManualCompletedAsync(string contentContentItemId, int page, int pageSize, CancellationToken cancellationToken = default)
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
            Count = await query.CountAsync(cancellationToken),
            Entries = (await query.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<OmnichannelActivity> GetAsync(string channel, string channelEndpointId, string preferredDestination, ActivityInteractionType interactionType, CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(cancellationToken);
    }
}
