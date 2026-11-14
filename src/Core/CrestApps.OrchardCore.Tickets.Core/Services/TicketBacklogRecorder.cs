using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Takes the daily backlog snapshot: for each desk, the tickets still open at the start of the day in the site's time
/// zone, counted by status, priority, team, assignee, ticket type and channel. A desk's day is taken once; a later run
/// on the same day leaves it as it is.
/// </summary>
public sealed class TicketBacklogRecorder
{
    /// <summary>
    /// How many tickets are read at a time.
    /// </summary>
    public const int BatchSize = 500;

    private static readonly TicketStatusCategory[] _open =
    [
        TicketStatusCategory.New,
        TicketStatusCategory.Open,
        TicketStatusCategory.Pending,
        TicketStatusCategory.OnHold,
    ];

    private readonly ISession _session;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBacklogRecorder"/> class.
    /// </summary>
    public TicketBacklogRecorder(ISession session, ICatalog<TicketDesk> desks, IClock clock, ILocalClock localClock)
    {
        _session = session;
        _desks = desks;
        _clock = clock;
        _localClock = localClock;
    }

    /// <summary>
    /// Takes today's snapshot of every desk that has none yet.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many desks were recorded.</returns>
    public async Task<int> RecordAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var day = (await _localClock.ConvertToLocalAsync(new DateTimeOffset(now, TimeSpan.Zero))).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var recorded = 0;

        foreach (var desk in await _desks.GetAllAsync(cancellationToken))
        {
            var deskId = desk.ItemId;

            if (await _session.QueryIndex<TicketBacklogIndex>(index => index.Day == day && index.DeskId == deskId).CountAsync(cancellationToken) > 0)
            {
                continue;
            }

            var snapshot = await TakeAsync(deskId, now, cancellationToken);
            snapshot.Day = day;

            await _session.SaveAsync(snapshot, false, null, cancellationToken);
            recorded++;
        }

        return recorded;
    }

    /// <summary>
    /// Counts the open tickets of a desk as they are now.
    /// </summary>
    /// <param name="deskId">The desk.</param>
    /// <param name="utcNow">The time the snapshot is taken.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The snapshot, without its day.</returns>
    public async Task<TicketBacklogDay> TakeAsync(string deskId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var groups = new Dictionary<(string, TicketStatusCategory, string, string, string, string, string), TicketBacklogGroup>();
        var lastId = 0L;

        while (true)
        {
            var afterId = lastId;
            var page = await _session.QueryIndex<TicketIndex>(index => index.DeskId == deskId && index.StatusCategory.IsIn(_open) && index.MergedIntoId == null && index.Id > afterId)
                .OrderBy(index => index.Id)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            var count = 0;

            foreach (var ticket in page)
            {
                count++;
                lastId = ticket.Id;

                var key = (ticket.StatusId, ticket.StatusCategory, ticket.PriorityId, ticket.TeamId, ticket.AssigneeId, ticket.ContentType, ticket.Channel);

                if (!groups.TryGetValue(key, out var group))
                {
                    groups[key] = group = new TicketBacklogGroup
                    {
                        StatusId = ticket.StatusId,
                        StatusCategory = ticket.StatusCategory,
                        PriorityId = ticket.PriorityId,
                        TeamId = ticket.TeamId,
                        AssigneeId = ticket.AssigneeId,
                        ContentType = ticket.ContentType,
                        Channel = ticket.Channel,
                        OldestCreatedUtc = ticket.CreatedUtc,
                    };
                }

                group.Count++;
                group.TotalAgeHours += Math.Max(0, (utcNow - ticket.CreatedUtc).TotalHours);

                if (ticket.NextDueUtc is { } due && due < utcNow)
                {
                    group.Overdue++;
                }

                if (ticket.CreatedUtc < group.OldestCreatedUtc)
                {
                    group.OldestCreatedUtc = ticket.CreatedUtc;
                }
            }

            if (count < BatchSize)
            {
                break;
            }
        }

        return new TicketBacklogDay
        {
            DeskId = deskId,
            TakenUtc = utcNow,
            Groups = [.. groups.Values],
        };
    }
}
