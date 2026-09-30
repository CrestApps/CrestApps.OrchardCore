using CrestApps.Core.Services;
using CrestApps.Core.Support;
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
    private const int _countPageSize = 1000;

    private readonly ICatalog<OmnichannelActivityBatch> _catalog;
    private readonly ISession _session;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly IEnumerable<IActivityDialerContributor> _dialerContributors;
    private readonly ActivityBatchSourceOptions _sourceOptions;
    private readonly IContactOptOutResolver _optOutResolver;
    private readonly INotInServiceNumberService _notInServiceNumbers;
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
    /// <param name="dialerContributors">The optional dialer contributors.</param>
    /// <param name="sourceOptions">The configured activity batch sources.</param>
    /// <param name="optOutResolver">The resolver that decides whether a contact may be reached on a channel.</param>
    /// <param name="notInServiceNumbers">The list of numbers known not to be in service.</param>
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
        IEnumerable<IActivityDialerContributor> dialerContributors,
        IOptions<ActivityBatchSourceOptions> sourceOptions,
        IContactOptOutResolver optOutResolver,
        INotInServiceNumberService notInServiceNumbers,
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
        _dialerContributors = dialerContributors;
        _sourceOptions = sourceOptions.Value;
        _optOutResolver = optOutResolver;
        _notInServiceNumbers = notInServiceNumbers;
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

        // Whatever the previous load of this batch found belongs to that load. Cleared here as well as by the
        // coordinator, because a loader can be run without it and would otherwise add its counts to stale ones.
        batch.ResetLoadCounts();

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

        ActivityDialerProfileDescriptor dialerProfile = null;
        var dialerContributor = _dialerContributors.FirstOrDefault();

        if (string.Equals(sourceEntry.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch.DialerProfileId))
            {
                batch.Status = OmnichannelActivityBatchStatus.New;

                await _catalog.UpdateAsync(batch, cancellationToken);

                _logger.LogError("A dialer profile is required before loading the dialer batch with ID '{BatchId}'.", batch.ItemId);
                return;
            }

            if (dialerContributor is null)
            {
                batch.Status = OmnichannelActivityBatchStatus.New;

                await _catalog.UpdateAsync(batch, cancellationToken);

                _logger.LogError("The Contact Center dialer services are not available for the dialer batch with ID '{BatchId}'.", batch.ItemId);
                return;
            }

            dialerProfile = await dialerContributor.FindByIdAsync(batch.DialerProfileId.Trim(), cancellationToken);

            if (dialerProfile is null)
            {
                batch.Status = OmnichannelActivityBatchStatus.New;

                await _catalog.UpdateAsync(batch, cancellationToken);

                _logger.LogError("Unable to find the dialer profile '{DialerProfileId}' for the dialer batch with ID '{BatchId}'.", batch.DialerProfileId, batch.ItemId);
                return;
            }

            // Dialer activities are queued on their campaign's queue, which is also what agents sign in to. Without
            // a campaign every enqueue would fail after its activity was created, leaving a half-loaded batch, so
            // the load stops before creating anything. The load editor refuses this case; older or imported loads
            // can still reach it.
            if (string.IsNullOrWhiteSpace(batch.CampaignId) && string.IsNullOrWhiteSpace(flowSettings.CampaignId))
            {
                batch.Status = OmnichannelActivityBatchStatus.New;

                await _catalog.UpdateAsync(batch, cancellationToken);

                _logger.LogWarning("The dialer batch with ID '{BatchId}' was not loaded because it has no campaign and its subject '{SubjectContentType}' has no default campaign, so its activities could not be queued for dialing. Choose a campaign on the inventory load or set a default campaign on the subject.", batch.ItemId, batch.SubjectContentType);
                return;
            }
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
                if (!PhoneNumberSearchTerm.TryParse(batch.PhoneNumber, out var searchTerm))
                {
                    phoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _logger.LogWarning("The phone number filter for activity batch '{BatchId}' does not contain any digits.", batch.ItemId);
                }
                else
                {
                    var phoneQuery = batch.OnlyPublishedLeads
                        ? readonlySession.QueryIndex<OmnichannelContactIndex>(index => index.Published)
                        : readonlySession.QueryIndex<OmnichannelContactIndex>(index => index.Latest);

                    phoneQuery = phoneQuery.Where(OmnichannelContactPhonePredicates.Match(searchTerm, batch.PhoneNumberMatchType));

                    var phoneContacts = await phoneQuery.ListAsync(cancellationToken);
                    phoneIds = phoneContacts.Select(c => c.ContentItemId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (hasTimeZoneFilter)
            {
                var timeZoneQuery = batch.OnlyPublishedLeads
                    ? readonlySession.QueryIndex<OmnichannelContactIndex>(index => index.Published)
                    : readonlySession.QueryIndex<OmnichannelContactIndex>(index => index.Latest);

                var tzContacts = await timeZoneQuery
                    .Where(index => index.TimeZoneId.IsIn(batch.TimeZoneIds))
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
                // Said out loud, because a batch that settles at Loaded with nothing in it looks exactly like one
                // that loaded everybody it should have. Which filters were set is enough to start from; the values
                // themselves can be phone numbers and stay out of the log.
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "No contacts matched the filters of the activity batch '{BatchId}', so it was marked loaded without creating any activity. Phone filter: {HasPhoneFilter}, time zone filter: {HasTimeZoneFilter}, last activity filter: {HasLastActivityFilter}.",
                        batch.ItemId.SanitizeLogValue(),
                        hasPhoneFilter,
                        hasTimeZoneFilter,
                        hasLastActivityFilter);
                }

                batch.Status = OmnichannelActivityBatchStatus.Loaded;

                await _catalog.UpdateAsync(batch, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var activityCounter = 0;
        var limitReached = false;

        while (!limitReached)
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
                break;
            }

            // The numbers already found dead, for every contact on this page, in one query rather than one per contact.
            var notInServiceNumbers = await _notInServiceNumbers.GetNotInServiceAsync(
                contacts.SelectMany(OmnichannelHelper.GetPhoneNumbers),
                cancellationToken);

            var preventDuplicates = batch.PreventDuplicates;

            HashSet<string> inQueueActivities = null;

            if (preventDuplicates)
            {
                var contentItemsIds = contacts.Select(x => x.ContentItemId).ToArray();

                // A contact counts as a duplicate only while it still has an OPEN activity. Every terminal state
                // is excluded so a finished contact can be re-loaded: a completed or purged activity was already
                // excluded, and a failed or cancelled one is just as terminal — leaving those in would let a
                // single failed dial (for example a busy or no-answer) permanently bar the lead from ever being
                // loaded again.
                //
                // Only an open activity for this batch's subject counts, which is what the option has always said
                // it does. Counting every subject meant a contact with an unrelated open task -- a callback, a
                // service follow-up -- silently fell out of every campaign load. The campaign and channel do not
                // narrow it further: two agents working the same subject with the same person is the duplicate the
                // option exists to prevent, whichever campaign or channel each one came through.
                var subjectContentType = batch.SubjectContentType;

                inQueueActivities = (await readonlySession.QueryIndex<OmnichannelActivityIndex>(index =>
                    index.ContactContentType == batch.ContactContentType &&
                    index.SubjectContentType == subjectContentType &&
                    index.ContactContentItemId.IsIn(contentItemsIds) &&
                    index.Status != ActivityStatus.Completed &&
                    index.Status != ActivityStatus.Purged &&
                    index.Status != ActivityStatus.Failed &&
                    index.Status != ActivityStatus.Cancelled, collection: OmnichannelConstants.CollectionName)
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

                // Respect the limit if specified. The contacts it cuts are counted rather than examined, so a
                // small trial load over a large list does not have to read the rest of the list to report on it.
                if (batch.Limit.HasValue && batch.Limit.Value > 0 && batch.TotalLoaded >= batch.Limit.Value)
                {
                    var remaining = await CountRemainingMatchesAsync(
                        readonlySession,
                        batch,
                        contact.Id,
                        leadCreatedFrom,
                        leadCreatedTo,
                        eligibleContactIds,
                        cancellationToken);

                    batch.TotalMatched += remaining;
                    batch.TotalSkippedByLimit += remaining;
                    limitReached = true;

                    break;
                }

                batch.TotalMatched++;

                if (preventDuplicates && inQueueActivities.Contains(contact.ContentItemId))
                {
                    batch.TotalSkippedAsDuplicate++;

                    continue;
                }

                var user = requiresUserAssignment
                    ? users[activityCounter++ % users.Length]
                    : null;

                var activity = await _activityManager.NewAsync(cancellationToken: cancellationToken);
                var activitySource = sourceEntry.Source;
                var channel = string.IsNullOrWhiteSpace(batch.Channel) ? flowSettings.Channel : batch.Channel;
                var channelEndpointId = string.IsNullOrWhiteSpace(batch.ChannelEndpointId) ? flowSettings.ChannelEndpointId : batch.ChannelEndpointId;
                var campaignId = string.IsNullOrWhiteSpace(batch.CampaignId) ? flowSettings.CampaignId : batch.CampaignId;
                var interactionType = string.Equals(sourceEntry.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase)
                    ? ActivityInteractionType.Automated
                    : ActivityInteractionType.Manual;
                var automatedSettings = OmnichannelAutomationHelper.ResolveActivitySettings(batch, flowSettings);

                if (dialerProfile is not null)
                {
                    // The campaign comes from the batch (resolved above); the profile only decides how the
                    // contacts are dialed, so it no longer overrides which campaign they belong to.
                    activitySource = dialerProfile.ActivitySource;
                    interactionType = ActivityInteractionType.Manual;
                    channel = OmnichannelConstants.Channels.Phone;
                    automatedSettings.AIProfileId = null;
                    automatedSettings.SpeechToTextDeploymentName = null;
                    automatedSettings.TextToSpeechDeploymentName = null;
                    automatedSettings.TextToSpeechVoiceId = null;
                    automatedSettings.UseCallAmbience = false;
                    automatedSettings.AllowAIToUpdateContact = false;
                    automatedSettings.AllowAIToUpdateSubject = false;
                    automatedSettings.ResponseDelayMode = OmnichannelResponseDelayMode.None;
                    automatedSettings.ResponseDelaySeconds = 0;
                    automatedSettings.ResponseDelayJitterSeconds = 0;
                    automatedSettings.BusinessHoursCalendarId = null;
                    automatedSettings.CadenceId = null;
                }

                activity.Kind = GetActivityKind(channel);
                activity.Source = activitySource;
                activity.InteractionType = interactionType;
                activity.Channel = channel;
                activity.AIProfileId = automatedSettings.AIProfileId;
                activity.SpeechToTextDeploymentName = automatedSettings.SpeechToTextDeploymentName;
                activity.TextToSpeechDeploymentName = automatedSettings.TextToSpeechDeploymentName;
                activity.TextToSpeechVoiceId = automatedSettings.TextToSpeechVoiceId;
                activity.UseCallAmbience = automatedSettings.UseCallAmbience;
                activity.AllowAIToUpdateContact = automatedSettings.AllowAIToUpdateContact;
                activity.AllowAIToUpdateSubject = automatedSettings.AllowAIToUpdateSubject;
                activity.ResponseDelayMode = automatedSettings.ResponseDelayMode;
                activity.ResponseDelaySeconds = automatedSettings.ResponseDelaySeconds;
                activity.ResponseDelayJitterSeconds = automatedSettings.ResponseDelayJitterSeconds;
                activity.BusinessHoursCalendarId = automatedSettings.BusinessHoursCalendarId;
                activity.CadenceId = automatedSettings.CadenceId;
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = batch.ContactContentType;
                activity.SubjectContentType = batch.SubjectContentType;
                // Asked before a destination is looked for, and asked whatever kind of activity this is. The
                // check underneath excludes an opted-out contact only from automated work, which left an agent
                // being handed a call sheet containing people who had asked not to be called -- the obligation is
                // the same whoever ends up dialling. The batch's own "include" flag is the operator's explicit
                // override for the cases a preference is not meant to block, such as a recall notice; until now
                // it was stored, editable and read by nothing at all.
                //
                // Asked of everybody reachable at the contact's numbers, not only of this record: live, a contact
                // who had asked not to be called shared a number with a second record, and the second record was
                // dialled on its other number. Whoever asked to stop may not be reached at any number that leads
                // to them.
                //
                // The contact's own preference is read first so the report can tell the two apart: an operator who
                // sees a whole list excluded needs to know it was one opted-out record sharing everybody's number.
                if (!IncludesOptedOutContacts(batch, activity.Channel))
                {
                    if (OmnichannelContactPreferences.HasOptedOut(contact, activity.Channel))
                    {
                        batch.TotalSkippedAsOptedOut++;

                        continue;
                    }

                    if (await _optOutResolver.HasOptedOutAsync(contact, activity.Channel, cancellationToken))
                    {
                        batch.TotalSkippedAsSharedNumberOptedOut++;

                        continue;
                    }
                }

                // The consent question was settled on the line above, so this only has to find the address. Asking
                // the version that answers both would refuse an address to exactly the contacts the operator just
                // said to include, and the override would do nothing.
                //
                // A number already found not to be in service is passed over for the contact's next one. A contact
                // whose every number on the channel is dead is not loaded at all: there is nobody to reach, and a call
                // sheet or dialer queue full of dead numbers is exactly the work this exists to save.
                var usesPhoneNumbers = activity.Channel is OmnichannelConstants.Channels.Phone or OmnichannelConstants.Channels.Sms;

                activity.PreferredDestination = usesPhoneNumbers
                    ? OmnichannelHelper.FindDestination(contact, activity.Channel, notInServiceNumbers.Contains)
                    : OmnichannelHelper.FindDestination(contact, activity.Channel);

                if (usesPhoneNumbers &&
                    string.IsNullOrWhiteSpace(activity.PreferredDestination) &&
                    notInServiceNumbers.Count > 0 &&
                    !string.IsNullOrWhiteSpace(OmnichannelHelper.FindDestination(contact, activity.Channel)))
                {
                    batch.TotalSkippedAsNotInService++;

                    continue;
                }

                if (activity.InteractionType == ActivityInteractionType.Automated &&
                    string.IsNullOrWhiteSpace(activity.PreferredDestination))
                {
                    batch.TotalSkippedForNoDestination++;

                    continue;
                }

                activity.ChannelEndpointId = channelEndpointId;
                activity.CampaignId = campaignId;
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

                if (dialerProfile is not null)
                {
                    // Queueing for the dialer commits the unit of work, and a committed session no longer tracks what
                    // it loaded before: the batch saved after it would be stored as a second document. The batch goes
                    // into this commit, and is read again as the commit left it.
                    await _catalog.UpdateAsync(batch, cancellationToken);

                    await dialerContributor.EnqueueAsync(
                        activity.ItemId,
                        campaignId,
                        dialerProfile,
                        cancellationToken);

                    batch = await _catalog.FindByIdAsync(batch.ItemId, cancellationToken) ?? batch;
                }
            }

            await _catalog.UpdateAsync(batch, cancellationToken);

            // Flush the session to release memory.
            await _session.FlushAsync(cancellationToken);
        }

        // Complete the batch loading.
        batch.Status = OmnichannelActivityBatchStatus.Loaded;

        await _catalog.UpdateAsync(batch, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        // One line per load that accounts for every matching contact, so a load that found fewer people than
        // expected can be explained from the log alone. Counts only: names and numbers stay out of it.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Loaded {TotalLoaded} of {TotalMatched} matching contacts for the activity batch '{BatchId}' and subject '{SubjectContentType}'. Skipped: {SkippedAsDuplicate} already had an open activity for the subject, {SkippedAsOptedOut} had opted out of the channel, {SkippedAsSharedNumberOptedOut} shared a number with a contact who had opted out, {SkippedForNoDestination} had no destination on the channel, {SkippedAsNotInService} had only numbers that are not in service, {SkippedByLimit} were over the limit.",
                batch.TotalLoaded ?? 0,
                batch.TotalMatched ?? 0,
                batch.ItemId.SanitizeLogValue(),
                batch.SubjectContentType.SanitizeLogValue(),
                batch.TotalSkippedAsDuplicate,
                batch.TotalSkippedAsOptedOut,
                batch.TotalSkippedAsSharedNumberOptedOut,
                batch.TotalSkippedForNoDestination,
                batch.TotalSkippedAsNotInService,
                batch.TotalSkippedByLimit);
        }
    }

    /// <summary>
    /// Counts the matching contacts from <paramref name="fromDocumentId"/> onwards, which are the ones a reached limit
    /// leaves unexamined.
    /// </summary>
    /// <remarks>
    /// Only the index is read. Without a pre-computed filter set the database counts the rows itself; with one, the
    /// index rows are paged so that neither the contacts nor an unbounded id list are held in memory at once.
    /// </remarks>
    private static async Task<long> CountRemainingMatchesAsync(
        ISession readonlySession,
        OmnichannelActivityBatch batch,
        long fromDocumentId,
        DateTime? leadCreatedFrom,
        DateTime? leadCreatedTo,
        HashSet<string> eligibleContactIds,
        CancellationToken cancellationToken)
    {
        if (eligibleContactIds is null)
        {
            return await BuildContactIndexQuery(readonlySession, batch, fromDocumentId - 1, leadCreatedFrom, leadCreatedTo)
                .CountAsync(cancellationToken);
        }

        long count = 0;
        var cursor = fromDocumentId - 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rows = (await BuildContactIndexQuery(readonlySession, batch, cursor, leadCreatedFrom, leadCreatedTo)
                .OrderBy(index => index.DocumentId)
                .Take(_countPageSize)
                .ListAsync(cancellationToken))
                .ToArray();

            foreach (var row in rows)
            {
                cursor = Math.Max(cursor, row.DocumentId);

                if (eligibleContactIds.Contains(row.ContentItemId))
                {
                    count++;
                }
            }

            if (rows.Length < _countPageSize)
            {
                return count;
            }
        }
    }

    /// <summary>
    /// The index-only twin of the contact page query, with the same content type, creation window and version rules.
    /// </summary>
    private static IQueryIndex<ContentItemIndex> BuildContactIndexQuery(
        ISession readonlySession,
        OmnichannelActivityBatch batch,
        long afterDocumentId,
        DateTime? leadCreatedFrom,
        DateTime? leadCreatedTo)
    {
        var contactContentType = batch.ContactContentType;
        var query = readonlySession.QueryIndex<ContentItemIndex>(index =>
            index.ContentType == contactContentType &&
            index.DocumentId > afterDocumentId);

        if (leadCreatedFrom.HasValue)
        {
            query = query.Where(index => index.CreatedUtc >= leadCreatedFrom);
        }

        if (leadCreatedTo.HasValue)
        {
            query = query.Where(index => index.CreatedUtc <= leadCreatedTo);
        }

        return batch.OnlyPublishedLeads
            ? query.Where(index => index.Published)
            : query.Where(index => index.Latest);
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

    /// <summary>
    /// Whether this batch was told to load contacts who have asked not to be reached on this channel.
    /// </summary>
    /// <remarks>
    /// Unticked is the form's way of saying "respect the preference", so the default excludes them. Ticking it is
    /// a deliberate act by somebody who has decided this particular message is not the kind the preference is
    /// meant to stop -- and it is per channel, because agreeing to a text is not agreeing to a call.
    /// </remarks>
    private static bool IncludesOptedOutContacts(OmnichannelActivityBatch batch, string channel)
    {
        if (channel == OmnichannelConstants.Channels.Phone)
        {
            return batch.IncludeDoNoCalls;
        }

        if (channel == OmnichannelConstants.Channels.Sms)
        {
            return batch.IncludeDoNoSms;
        }

        if (channel == OmnichannelConstants.Channels.Email)
        {
            return batch.IncludeDoNoEmail;
        }

        return false;
    }
}
