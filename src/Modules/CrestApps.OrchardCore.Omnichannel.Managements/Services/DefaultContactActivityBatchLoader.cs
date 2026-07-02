using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The default activity batch loader. It pages over contacts of the batch contact content type,
/// applies the batch filters, and creates activities using the configured subject flow settings.
/// This loader is used as the fallback for any source that does not register a dedicated
/// <see cref="IActivityBatchLoader"/>. It is not sealed so specialized sources can inherit and
/// customize individual stages of the load.
/// </summary>
public class DefaultContactActivityBatchLoader : IActivityBatchLoader
{
    private const int _batchSize = 100;

    private readonly ICatalog<OmnichannelActivityBatch> _catalog;
    private readonly ISession _session;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ActivityBatchSourceOptions _sourceOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContactActivityBatchLoader"/> class.
    /// </summary>
    /// <param name="catalog">The activity batch catalog.</param>
    /// <param name="session">The session used to persist activities.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="store">The store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="sourceOptions">The configured activity batch sources.</param>
    /// <param name="logger">The logger.</param>
    public DefaultContactActivityBatchLoader(
        ICatalog<OmnichannelActivityBatch> catalog,
        ISession session,
        ILocalClock localClock,
        IClock clock,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IOmnichannelActivityManager activityManager,
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        IOptions<ActivityBatchSourceOptions> sourceOptions,
        ILogger<DefaultContactActivityBatchLoader> logger)
    {
        _catalog = catalog;
        _session = session;
        _localClock = localClock;
        _clock = clock;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _activityManager = activityManager;
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
        _sourceOptions = sourceOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public virtual string Source
        => null;

    /// <inheritdoc />
    public virtual async Task LoadAsync(ActivityBatchLoadContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var batch = context.Batch;

        if (!TryGetActivityBatchSource(batch.Source, _sourceOptions, out var sourceEntry))
        {
            batch.Status = OmnichannelActivityBatchStatus.New;

            await _catalog.UpdateAsync(batch, cancellationToken);

            _logger.LogError("No valid activity batch source was found for the batch with ID '{BatchId}' and source '{Source}'.", batch.ItemId, batch.Source);
            return;
        }

        await using var readonlySession = _session.Store.CreateSession(withTracking: false);

        var requiresUserAssignment = sourceEntry.RequiresUserAssignment;
        var users = requiresUserAssignment
            ? (await readonlySession.Query<User, UserIndex>(x => x.IsEnabled && x.UserId.IsIn(batch.UserIds)).ListAsync(cancellationToken)).ToArray()
            : [];

        if (requiresUserAssignment && users.Length == 0)
        {
            batch.Status = OmnichannelActivityBatchStatus.New;

            await _catalog.UpdateAsync(batch, cancellationToken);

            _logger.LogError("No valid users were found to assign the activities for the batch with ID '{BatchId}'.", batch.ItemId);
            return;
        }

        var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(batch.SubjectContentType, cancellationToken);

        if (flowSettings is null)
        {
            batch.Status = OmnichannelActivityBatchStatus.New;

            await _catalog.UpdateAsync(batch, cancellationToken);

            _logger.LogError("Configured subject flow settings are required before loading the batch with ID '{BatchId}' for subject '{SubjectContentType}'.", batch.ItemId, batch.SubjectContentType);
            return;
        }

        long documentId = 0;

        DateTime? leadCreatedFrom = batch.LeadCreatedFrom.HasValue
            ? await _localClock.ConvertToUtcAsync(batch.LeadCreatedFrom.Value)
            : null;

        DateTime? leadCreatedTo = batch.LeadCreatedTo.HasValue
            ? await _localClock.ConvertToUtcAsync(batch.LeadCreatedTo.Value)
            : null;

        // Pre-compute contact-level filter sets (phone, timezone, last activity).
        HashSet<string> eligibleContactIds = null;

        var hasPhoneFilter = !string.IsNullOrEmpty(batch.PhoneNumber);
        var hasTimeZoneFilter = batch.TimeZoneIds is { Length: > 0 };
        var hasLastActivityFilter = !string.IsNullOrEmpty(batch.LastActivitySubjectContentType);

        if (hasPhoneFilter || hasTimeZoneFilter || hasLastActivityFilter)
        {
            HashSet<string> phoneIds = null;
            HashSet<string> timeZoneIds = null;
            HashSet<string> lastActivityIds = null;

            if (hasPhoneFilter)
            {
                var phoneQuery = batch.PhoneNumberMatchType switch
                {
                    PhoneNumberMatchType.Exact => readonlySession.QueryIndex<OmnichannelContactIndex>(index =>
                        index.NormalizedPrimaryCellPhoneNumber == batch.PhoneNumber ||
                        index.NormalizedPrimaryHomePhoneNumber == batch.PhoneNumber),
                    PhoneNumberMatchType.EndsWith => readonlySession.QueryIndex<OmnichannelContactIndex>(index =>
                        index.NormalizedPrimaryCellPhoneNumber.EndsWith(batch.PhoneNumber) ||
                        index.NormalizedPrimaryHomePhoneNumber.EndsWith(batch.PhoneNumber)),
                    _ => readonlySession.QueryIndex<OmnichannelContactIndex>(index =>
                        index.NormalizedPrimaryCellPhoneNumber.StartsWith(batch.PhoneNumber) ||
                        index.NormalizedPrimaryHomePhoneNumber.StartsWith(batch.PhoneNumber)),
                };

                var phoneContacts = await phoneQuery.ListAsync(cancellationToken);
                phoneIds = phoneContacts.Select(c => c.ContentItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (hasTimeZoneFilter)
            {
                var tzContacts = await readonlySession.QueryIndex<OmnichannelContactIndex>(
                    index => index.TimeZoneId.IsIn(batch.TimeZoneIds))
                    .ListAsync(cancellationToken);

                timeZoneIds = tzContacts.Select(c => c.ContentItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (hasLastActivityFilter)
            {
                // Use raw SQL to find contacts whose most recent completed activity
                // matches the given subject (and optional disposition). This avoids
                // materializing all completed activities in memory.
                var dialect = _store.Configuration.SqlDialect;
                var dbSchema = _store.Configuration.Schema;
                var activityTableName = _store.Configuration.TableNameConvention.GetIndexTable(
                    typeof(OmnichannelActivityIndex),
                    OmnichannelConstants.CollectionName);
                var activityTable = dialect.QuoteForTableName(
                    $"{_store.Configuration.TablePrefix}{activityTableName}",
                    dbSchema);
                var contactCol = dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ContactContentItemId));
                var statusCol = dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Status));
                var subjectCol = dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.SubjectContentType));
                var dispositionCol = dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.DispositionId));
                var completedCol = dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.CompletedUtc));

                var completedStatus = (int)ActivityStatus.Completed;

                // Find contacts where the most recent completed activity matches the subject/disposition.
                // Uses a correlated subquery to find the "latest per group" server-side.
                var sql = $@"SELECT DISTINCT a.{contactCol}
                            FROM {activityTable} a
                            WHERE a.{statusCol} = @CompletedStatus
                              AND a.{subjectCol} = @Subject
                              AND a.{completedCol} = (
                                  SELECT MAX(a2.{completedCol})
                                  FROM {activityTable} a2
                                  WHERE a2.{contactCol} = a.{contactCol}
                                    AND a2.{statusCol} = @CompletedStatus
                              )";

                var parameters = new DynamicParameters();
                parameters.Add("@CompletedStatus", completedStatus);
                parameters.Add("@Subject", batch.LastActivitySubjectContentType);

                if (!string.IsNullOrEmpty(batch.LastActivityDispositionId))
                {
                    sql += $"\n  AND a.{dispositionCol} = @Disposition";
                    parameters.Add("@Disposition", batch.LastActivityDispositionId);
                }

                await using var sqlConnection = _dbConnectionAccessor.CreateConnection();
                await sqlConnection.OpenAsync(cancellationToken);

                var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
                var results = await sqlConnection.QueryAsync<string>(command);

                lastActivityIds = results
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            // Intersect all non-null filter sets.
            foreach (var set in new[] { phoneIds, timeZoneIds, lastActivityIds })
            {
                if (set is null)
                {
                    continue;
                }

                if (eligibleContactIds is null)
                {
                    eligibleContactIds = set;
                }
                else
                {
                    eligibleContactIds.IntersectWith(set);
                }
            }

            // If filters are applied but no contacts match, mark as loaded immediately.
            if (eligibleContactIds is not null && eligibleContactIds.Count == 0)
            {
                batch.Status = OmnichannelActivityBatchStatus.Loaded;

                await _catalog.UpdateAsync(batch, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var activityCounter = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contactQuery = readonlySession.Query<ContentItem, ContentItemIndex>(index =>
                    index.ContentType == batch.ContactContentType &&
                    index.DocumentId > documentId);

            if (leadCreatedFrom.HasValue)
            {
                contactQuery = contactQuery.Where(index => index.CreatedUtc >= leadCreatedFrom);
            }

            if (leadCreatedTo.HasValue)
            {
                contactQuery = contactQuery.Where(index => index.CreatedUtc <= leadCreatedTo);
            }

            if (batch.OnlyPublishedLeads)
            {
                contactQuery = contactQuery.Where(contact => contact.Published);
            }
            else
            {
                contactQuery = contactQuery.Where(contact => contact.Latest);
            }

            var contacts = await contactQuery
                .OrderBy(x => x.DocumentId)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!contacts.Any())
            {
                batch.Status = OmnichannelActivityBatchStatus.Loaded;

                await _catalog.UpdateAsync(batch, cancellationToken);
                break;
            }

            var preventDuplicates = batch.PreventDuplicates;

            HashSet<string> inQueueActivities = null;

            if (preventDuplicates)
            {
                var contentItemsIds = contacts.Select(x => x.ContentItemId).ToArray();

                inQueueActivities = (await readonlySession.QueryIndex<OmnichannelActivityIndex>(index =>
                    index.ContactContentType == batch.ContactContentType &&
                    index.ContactContentItemId.IsIn(contentItemsIds) &&
                    index.Status != ActivityStatus.Completed &&
                    index.Status != ActivityStatus.Purged, collection: OmnichannelConstants.CollectionName)
                .ListAsync(cancellationToken))
                .Select(x => x.ContactContentItemId)
                .ToHashSet();
            }

            var now = _clock.UtcNow;

            var scheduledUtc = await _localClock.ConvertToUtcAsync(batch.ScheduleAt);

            foreach (var contact in contacts)
            {
                documentId = Math.Max(documentId, contact.Id);

                // Skip contacts not in the pre-computed eligible set.
                if (eligibleContactIds is not null && !eligibleContactIds.Contains(contact.ContentItemId))
                {
                    continue;
                }

                if (preventDuplicates && inQueueActivities.Contains(contact.ContentItemId))
                {
                    continue;
                }

                // Respect the limit if specified.
                if (batch.Limit.HasValue && batch.Limit.Value > 0 && batch.TotalLoaded >= batch.Limit.Value)
                {
                    batch.Status = OmnichannelActivityBatchStatus.Loaded;

                    await _catalog.UpdateAsync(batch, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);
                    return;
                }

                var user = requiresUserAssignment
                    ? users[activityCounter++ % users.Length]
                    : null;

                var activity = await _activityManager.NewAsync(cancellationToken: cancellationToken);

                activity.Kind = GetActivityKind(flowSettings.Channel);
                activity.Source = sourceEntry.Source;
                activity.InteractionType = flowSettings.InteractionType;
                activity.Channel = flowSettings.Channel;
                activity.AIProfileId = string.IsNullOrWhiteSpace(batch.AIProfileId)
                    ? flowSettings.ProfileId
                    : batch.AIProfileId;
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = batch.ContactContentType;
                activity.SubjectContentType = batch.SubjectContentType;
                activity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(contact, activity.Channel);

                if (activity.InteractionType == ActivityInteractionType.Automated &&
                    string.IsNullOrWhiteSpace(activity.PreferredDestination))
                {
                    continue;
                }

                activity.ChannelEndpointId = flowSettings.ChannelEndpointId;
                activity.CampaignId = flowSettings.CampaignId;
                activity.ScheduledUtc = scheduledUtc;
                if (user is not null)
                {
                    activity.AssignedToId = user.UserId;
                    activity.AssignedToUsername = user.UserName;
                    activity.AssignedToUtc = now;
                    activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
                }
                else
                {
                    activity.AssignmentStatus = ActivityAssignmentStatus.Available;
                }

                activity.Instructions = batch.Instructions;
                activity.CreatedUtc = now;
                activity.CreatedById = context.LoaderId;
                activity.CreatedByUsername = context.LoaderUserName;
                activity.UrgencyLevel = batch.UrgencyLevel;
                activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(
                    activity.InteractionType,
                    user is not null);

                batch.TotalLoaded++;

                await _activityManager.CreateAsync(activity, cancellationToken);
                await _session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            }

            await _catalog.UpdateAsync(batch, cancellationToken);

            // Flush the session to release memory.
            await _session.FlushAsync(cancellationToken);
        }

        // Complete the batch loading.
        batch.Status = OmnichannelActivityBatchStatus.Loaded;

        await _catalog.UpdateAsync(batch, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private static bool TryGetActivityBatchSource(string source, ActivityBatchSourceOptions options, out ActivityBatchSourceEntry sourceEntry)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            sourceEntry = null;

            return false;
        }

        var normalizedSource = source.Trim();

        return options.Sources.TryGetValue(normalizedSource, out sourceEntry);
    }

    private static ActivityKind GetActivityKind(string channel)
    {
        if (string.Equals(channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase))
        {
            return ActivityKind.Call;
        }

        if (string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
        {
            return ActivityKind.Sms;
        }

        if (string.Equals(channel, OmnichannelConstants.Channels.Email, StringComparison.OrdinalIgnoreCase))
        {
            return ActivityKind.Email;
        }

        return ActivityKind.Task;
    }
}
