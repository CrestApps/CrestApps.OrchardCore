using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Indexes the routing decisions and dial suppressions written before the dialer decision log existed.
/// </summary>
/// <remarks>
/// Saving an event unchanged writes its index rows again, now including the decision log's. Only events without a
/// decision row are saved, so the events written since the log was added, which were indexed as they were written,
/// are only read. A suppression written before suppressions named their campaign is given its record's campaign as
/// it is saved, so the report's campaign filter finds it.
/// </remarks>
internal static class DialerDecisionIndexBackfill
{
    /// <summary>
    /// The most events read in one page.
    /// </summary>
    public const int PageSize = 200;

    /// <summary>
    /// Gets the event streams the backfill indexes, each with the name its progress is remembered under.
    /// </summary>
    public static IReadOnlyList<Source> Sources { get; } =
    [
        new("DialerDecisionIndex:RoutingDecisionMade", ContactCenterConstants.Events.RoutingDecisionMade, nameof(QueueItem)),
        new("DialerDecisionIndex:DialSuppressed", ContactCenterConstants.Events.DialSuppressed, nameof(OmnichannelActivity)),
    ];

    /// <summary>
    /// Indexes up to <paramref name="maxPages"/> pages of one event stream, starting after <paramref name="cursor"/>.
    /// </summary>
    /// <param name="store">The YesSql store; each page is read and written in a session of its own.</param>
    /// <param name="source">The event stream to index.</param>
    /// <param name="cursor">The last event already read, or <see langword="null"/> to start from the first.</param>
    /// <param name="maxPages">The most pages to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Where the backfill stopped, whether it reached the end, and how many events it indexed.</returns>
    public static async Task<Result> RunAsync(
        IStore store,
        Source source,
        ContactCenterBackfillCursor cursor,
        int maxPages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(source);

        var indexed = 0;

        for (var page = 0; page < maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var session = store.CreateSession();

            var rows = (await Page(session, source, cursor)
                .OrderBy(index => index.OccurredUtc)
                .ThenBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToArray();

            if (rows.Length == 0)
            {
                return new Result(cursor, Finished: true, indexed);
            }

            var documentIds = rows.Select(row => row.DocumentId).ToArray();
            var alreadyIndexed = (await session.QueryIndex<DialerDecisionIndex>(
                index => index.DocumentId.IsIn(documentIds),
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken))
                .Select(index => index.DocumentId)
                .ToHashSet();

            var missing = documentIds.Where(documentId => !alreadyIndexed.Contains(documentId)).ToArray();

            if (missing.Length > 0)
            {
                var events = (await session.Query<InteractionEvent, InteractionEventIndex>(
                    index => index.DocumentId.IsIn(missing),
                    collection: ContactCenterStorage.CollectionName)
                    .ListAsync(cancellationToken))
                    .ToArray();

                if (string.Equals(source.EventType, ContactCenterConstants.Events.DialSuppressed, StringComparison.Ordinal))
                {
                    await AddCampaignsAsync(session, events, cancellationToken);
                }

                foreach (var interactionEvent in events)
                {
                    await session.SaveAsync(interactionEvent, false, ContactCenterStorage.CollectionName, cancellationToken);
                }

                await session.SaveChangesAsync(cancellationToken);

                indexed += events.Length;
            }

            var last = rows[rows.Length - 1];

            cursor = new ContactCenterBackfillCursor
            {
                OccurredUtc = DateTime.SpecifyKind(last.OccurredUtc, DateTimeKind.Utc),
                DocumentId = last.DocumentId,
            };

            if (rows.Length < PageSize)
            {
                return new Result(cursor, Finished: true, indexed);
            }
        }

        return new Result(cursor, Finished: false, indexed);
    }

    private static IQueryIndex<InteractionEventIndex> Page(ISession session, Source source, ContactCenterBackfillCursor cursor)
    {
        var eventType = source.EventType;
        var aggregateType = source.AggregateType;

        if (cursor is null)
        {
            return session.QueryIndex<InteractionEventIndex>(
                index => index.AggregateType == aggregateType && index.EventType == eventType,
                collection: ContactCenterStorage.CollectionName);
        }

        var afterUtc = cursor.OccurredUtc;
        var afterDocumentId = cursor.DocumentId;

        // Read on the window index (aggregate type, time, event type), after the last event already read.
        return session.QueryIndex<InteractionEventIndex>(
            index => index.AggregateType == aggregateType &&
                index.EventType == eventType &&
                (index.OccurredUtc > afterUtc || (index.OccurredUtc == afterUtc && index.DocumentId > afterDocumentId)),
            collection: ContactCenterStorage.CollectionName);
    }

    // A suppression written before suppressions named their campaign is given its record's campaign, so filtering the
    // decision log by campaign finds it.
    private static async Task AddCampaignsAsync(ISession session, InteractionEvent[] events, CancellationToken cancellationToken)
    {
        var pending = new List<(InteractionEvent Event, DialerSuppressionEventData Data)>();

        foreach (var interactionEvent in events)
        {
            DialerSuppressionEventData data;

            try
            {
                data = interactionEvent.GetData<DialerSuppressionEventData>();
            }
            catch (JsonException)
            {
                continue;
            }

            if (data is not null && string.IsNullOrEmpty(data.CampaignId) && !string.IsNullOrEmpty(data.ActivityItemId ?? interactionEvent.AggregateId))
            {
                pending.Add((interactionEvent, data));
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        var activityIds = pending
            .Select(entry => entry.Data.ActivityItemId ?? entry.Event.AggregateId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var campaigns = (await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
            index => index.ItemId.IsIn(activityIds),
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken))
            .Where(activity => !string.IsNullOrEmpty(activity.CampaignId))
            .GroupBy(activity => activity.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().CampaignId, StringComparer.Ordinal);

        foreach (var (interactionEvent, data) in pending)
        {
            if (campaigns.TryGetValue(data.ActivityItemId ?? interactionEvent.AggregateId, out var campaignId))
            {
                data.CampaignId = campaignId;
                interactionEvent.SetData(data);
            }
        }
    }

    /// <summary>
    /// Represents one event stream the backfill indexes.
    /// </summary>
    /// <param name="Name">The name the backfill's progress is remembered under.</param>
    /// <param name="EventType">The event type.</param>
    /// <param name="AggregateType">The aggregate type the events are recorded against.</param>
    internal sealed record Source(string Name, string EventType, string AggregateType);

    /// <summary>
    /// Represents where a run of the backfill stopped.
    /// </summary>
    /// <param name="Cursor">The last event read.</param>
    /// <param name="Finished">Whether the end of the stream was reached.</param>
    /// <param name="Indexed">How many events were indexed.</param>
    internal sealed record Result(ContactCenterBackfillCursor Cursor, bool Finished, int Indexed);
}
