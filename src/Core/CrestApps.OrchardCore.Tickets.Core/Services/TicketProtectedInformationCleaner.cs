using CrestApps.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Deletes the protected information of tickets closed longer ago than their desk keeps it
/// (<see cref="TicketDesk.DeleteProtectedInformationDays"/>; 0 keeps it), and notes it on each ticket's timeline.
/// </summary>
public sealed class TicketProtectedInformationCleaner
{
    /// <summary>
    /// How many tickets one run looks at.
    /// </summary>
    public const int TicketsPerRun = 200;

    private readonly ISession _session;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ITicketProtectedInformation _protectedInformation;
    private readonly ITimelineService _timeline;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketProtectedInformationCleaner"/> class.
    /// </summary>
    public TicketProtectedInformationCleaner(
        ISession session,
        ICatalog<TicketDesk> desks,
        ITicketProtectedInformation protectedInformation,
        ITimelineService timeline,
        IClock clock,
        IStringLocalizer<TicketProtectedInformationCleaner> stringLocalizer)
    {
        _session = session;
        _desks = desks;
        _protectedInformation = protectedInformation;
        _timeline = timeline;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Deletes what is due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many tickets lost their protected information.</returns>
    public async Task<int> DeleteDueAsync(CancellationToken cancellationToken = default)
    {
        var days = (await _desks.GetAllAsync(cancellationToken))
            .Where(desk => desk.DeleteProtectedInformationDays > 0)
            .ToDictionary(desk => desk.ItemId, desk => desk.DeleteProtectedInformationDays, StringComparer.Ordinal);

        if (days.Count == 0)
        {
            return 0;
        }

        // The tickets that hold protected information, then those of them closed on a desk that deletes it.
        var ticketIds = (await _session.QueryIndex<TicketProtectedItemIndex>().ListAsync(cancellationToken))
            .Select(index => index.TicketId)
            .Distinct(StringComparer.Ordinal)
            .Take(TicketsPerRun)
            .ToArray();

        if (ticketIds.Length == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var deleted = 0;
        var closed = await _session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(ticketIds) && index.ClosedUtc != null).ListAsync(cancellationToken);

        foreach (var ticket in closed)
        {
            if (!days.TryGetValue(ticket.DeskId ?? string.Empty, out var keepDays) || ticket.ClosedUtc > now.AddDays(-keepDays))
            {
                continue;
            }

            if (await _protectedInformation.DeleteAllAsync(ticket.ContentItemId, cancellationToken) > 0)
            {
                deleted++;

                await _timeline.AddAsync(new TimelineEntry
                {
                    Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
                    Kind = TicketsConstants.EntryKinds.Event,
                    Visibility = TimelineVisibility.Internal,
                    Body = S["The protected information was deleted, {0} days after the ticket closed.", keepDays],
                }, cancellationToken);
            }
        }

        return deleted;
    }
}
