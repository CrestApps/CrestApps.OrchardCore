using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides, for many pending campaign records at once, what holds each one back. It reads each record's attempts, last
/// call and latest compliance suppression in batches and decides each through the evaluation the dispatch diagnostics use
/// (<see cref="DialerDispatchDiagnosticsService"/>), so the live dashboard, the dialer queue inventory and the campaign summary all give the reason the dispatcher acts on.
/// </summary>
internal sealed class DialerRecordClassifier
{
    private const int BatchSize = 500;

    private readonly Dictionary<string, DialerProfile> _profiles = new(StringComparer.Ordinal);
    private readonly IDialerProfileReader _profileReader;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateStore _workStateStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerRecordClassifier"/> class.
    /// </summary>
    /// <param name="profileReader">The reader of the dialer profile a record is dialed under.</param>
    /// <param name="activityManager">The CRM activities the campaign records are.</param>
    /// <param name="workStateStore">The work state that holds each record's attempt count.</param>
    /// <param name="eventStore">The recorded suppression decisions.</param>
    /// <param name="session">The YesSql session, which reads many records' last calls at once.</param>
    public DialerRecordClassifier(
        IDialerProfileReader profileReader,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateStore workStateStore,
        IInteractionEventStore eventStore,
        ISession session)
    {
        _profileReader = profileReader;
        _activityManager = activityManager;
        _workStateStore = workStateStore;
        _eventStore = eventStore;
        _session = session;
    }

    /// <summary>
    /// Reads a dialer profile once per classifier, however many records are stamped with it.
    /// </summary>
    /// <param name="profileId">The dialer profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The profile, or <see langword="null"/> when the identifier is empty, names the built-in queued-callback
    /// profile, or names a profile that no longer exists.</returns>
    public async Task<DialerProfile> ReadProfileAsync(string profileId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(profileId) || QueueCallbackDialerProfile.IsCallbackProfile(profileId))
        {
            return null;
        }

        if (!_profiles.TryGetValue(profileId, out var profile))
        {
            profile = await _profileReader.FindByIdAsync(profileId, cancellationToken);
            _profiles[profileId] = profile;
        }

        return profile;
    }

    /// <summary>
    /// Classifies the supplied records.
    /// </summary>
    /// <param name="records">The pending records to classify.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The verdict of each record, by activity identifier. A record whose activity can no longer be read has
    /// none.</returns>
    public async Task<IReadOnlyDictionary<string, DialerRecordVerdict>> ClassifyAsync(
        IReadOnlyCollection<DialerRecordInput> records,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);

        var verdicts = new Dictionary<string, DialerRecordVerdict>(StringComparer.Ordinal);

        if (records.Count == 0)
        {
            return verdicts;
        }

        var ids = records
            .Select(record => record.ActivityId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var activities = await LoadActivitiesAsync(ids, cancellationToken);
        var workStates = await LoadWorkStatesAsync(ids, cancellationToken);
        var lastInteractions = await LoadLastInteractionsAsync(ids, cancellationToken);
        var suppressions = await LoadSuppressionsAsync(ids, now, cancellationToken);

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.ActivityId) ||
                verdicts.ContainsKey(record.ActivityId) ||
                !activities.TryGetValue(record.ActivityId, out var activity))
            {
                continue;
            }

            workStates.TryGetValue(record.ActivityId, out var workState);
            lastInteractions.TryGetValue(record.ActivityId, out var lastInteraction);
            suppressions.TryGetValue(record.ActivityId, out var suppression);

            // The profile the record is dialed under: the one stamped on its queue item, or the activity's own when the
            // item carries none.
            var profileId = record.Queued
                ? record.StampedProfileId ?? record.ActivityProfileId
                : record.ActivityProfileId;

            var profile = await ReadProfileAsync(profileId, cancellationToken);
            var (blocker, dueUtc) = DialerDispatchDiagnosticsService.Evaluate(
                record.Queued,
                profileId,
                profile,
                activity,
                workState,
                lastInteraction,
                suppression,
                now);

            verdicts[record.ActivityId] = new DialerRecordVerdict(blocker, dueUtc, lastInteraction?.CreatedUtc);
        }

        return verdicts;
    }

    /// <summary>
    /// Reads an event's payload, treating one this release cannot read as absent.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="interactionEvent">The event.</param>
    /// <returns>The payload, or <see langword="null"/> when it cannot be read.</returns>
    internal static T ReadData<T>(InteractionEvent interactionEvent)
        where T : class
    {
        try
        {
            return interactionEvent.GetData<T>();
        }
        catch (JsonException)
        {
            // A payload written by a release this one cannot read says nothing about now.
            return null;
        }
    }

    private async Task<Dictionary<string, OmnichannelActivity>> LoadActivitiesAsync(string[] ids, CancellationToken cancellationToken)
    {
        var activities = new Dictionary<string, OmnichannelActivity>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            foreach (var activity in await _activityManager.GetByIdsAsync(batch, cancellationToken))
            {
                activities[activity.ItemId] = activity;
            }
        }

        return activities;
    }

    private async Task<Dictionary<string, ContactCenterWorkState>> LoadWorkStatesAsync(string[] ids, CancellationToken cancellationToken)
    {
        var states = new Dictionary<string, ContactCenterWorkState>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            foreach (var state in await _workStateStore.GetByActivityIdsAsync(batch, cancellationToken))
            {
                states[state.ActivityItemId] = state;
            }
        }

        return states;
    }

    private async Task<Dictionary<string, Interaction>> LoadLastInteractionsAsync(string[] ids, CancellationToken cancellationToken)
    {
        var last = new Dictionary<string, Interaction>(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(BatchSize))
        {
            var interactions = await _session.Query<Interaction, InteractionIndex>(
                index => index.ActivityItemId.IsIn(batch),
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken);

            foreach (var interaction in interactions)
            {
                if (!string.IsNullOrEmpty(interaction.ActivityItemId) &&
                    (!last.TryGetValue(interaction.ActivityItemId, out var existing) || interaction.CreatedUtc > existing.CreatedUtc))
                {
                    last[interaction.ActivityItemId] = interaction;
                }
            }
        }

        return last;
    }

    // The latest suppression of each record that no call has followed.
    private async Task<Dictionary<string, DialerSuppressionEventData>> LoadSuppressionsAsync(string[] ids, DateTime now, CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, (DateTime OccurredUtc, DialerSuppressionEventData Data)>(StringComparer.Ordinal);
        var events = await _eventStore.GetByAggregateWindowAsync(
            nameof(OmnichannelActivity),
            [ContactCenterConstants.Events.DialSuppressed],
            ids,
            now - DialerDispatchDiagnosticsService.SuppressionLookback,
            now,
            cancellationToken);

        foreach (var interactionEvent in events)
        {
            var data = ReadData<DialerSuppressionEventData>(interactionEvent);

            if (data is null || string.IsNullOrEmpty(interactionEvent.AggregateId))
            {
                continue;
            }

            if (!latest.TryGetValue(interactionEvent.AggregateId, out var existing) || interactionEvent.OccurredUtc >= existing.OccurredUtc)
            {
                latest[interactionEvent.AggregateId] = (interactionEvent.OccurredUtc, data);
            }
        }

        if (latest.Count == 0)
        {
            return [];
        }

        // A call placed after the suppression means the record was dialed since; the suppression no longer explains it.
        var lastInteractions = await LoadLastInteractionsAsync(latest.Keys.ToArray(), cancellationToken);
        var current = new Dictionary<string, DialerSuppressionEventData>(StringComparer.Ordinal);

        foreach (var (activityId, entry) in latest)
        {
            if (!lastInteractions.TryGetValue(activityId, out var interaction) || interaction.CreatedUtc <= entry.OccurredUtc)
            {
                current[activityId] = entry.Data;
            }
        }

        return current;
    }
}
