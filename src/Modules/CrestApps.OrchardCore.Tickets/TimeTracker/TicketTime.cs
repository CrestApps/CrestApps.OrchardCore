using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// Time on tickets, kept by Time Tracker: an agent starts working on a ticket, which switches their time to the desk's
/// ticket stage for that ticket (<see cref="TimeTrackerConstants.Sources.Ticket"/>, the ticket as the source ref), and
/// stops, which goes back to what they were doing before. A ticket's time is the entries for it.
/// </summary>
public sealed class TicketTime
{
    private const int StepsBack = 10;

    private readonly ITimeTrackerService _tracker;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TicketTime(ITimeTrackerService tracker, ICatalog<TicketDesk> desks, IClock clock, IStringLocalizer<TicketTime> stringLocalizer)
    {
        _tracker = tracker;
        _desks = desks;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// The record a ticket's time entries stand for.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>The source ref.</returns>
    public static string SourceRefOf(string ticketId)
        => TicketsConstants.RecordKind + ":" + ticketId;

    /// <summary>
    /// The stage a ticket's desk records work in.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The stage, or <see langword="null"/> when the desk records no time.</returns>
    public async Task<string> StageIdAsync(ContentItem ticket)
    {
        var deskId = ticket?.GetOrCreate<TicketPart>().DeskId;

        return string.IsNullOrEmpty(deskId) || await _desks.FindByIdAsync(deskId) is not { } desk
            ? null
            : desk.TryGet<TicketDeskTimeSettings>(out var settings) && !string.IsNullOrEmpty(settings.StageId) ? settings.StageId : null;
    }

    /// <summary>
    /// Starts a user's work on a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="userId">The user.</param>
    /// <param name="userName">Their name, as their time shows it.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or why it wasn't started.</returns>
    public async Task<TimeTrackerResult> StartAsync(ContentItem ticket, string userId, string userName, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (await StageIdAsync(ticket) is not { } stageId)
        {
            return TimeTrackerResult.Fail(S["This ticket's desk doesn't record time."]);
        }

        return await _tracker.SwitchAsync(userId, userName, stageId, TimeTrackerConstants.Sources.Ticket, actor, SourceRefOf(ticket.ContentItemId), cancellationToken);
    }

    /// <summary>
    /// Stops a user's work on a ticket: their time goes back to the stage they were in before they started on tickets,
    /// or they clock out when they started on it.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="userId">The user.</param>
    /// <param name="userName">Their name, as their time shows it.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry they are on now, or why nothing changed.</returns>
    public async Task<TimeTrackerResult> StopAsync(string ticketId, string userId, string userName, string actor, CancellationToken cancellationToken = default)
    {
        var status = await _tracker.GetStatusAsync(userId, cancellationToken);

        if (status?.Open is not { } open || open.SourceRef != SourceRefOf(ticketId))
        {
            return TimeTrackerResult.Fail(S["You aren't working on this ticket."]);
        }

        // Back through the entries that end where the next one starts, to the first that isn't ticket work.
        var entries = await _tracker.ListAsync(new TimeEntryQuery { UserIds = [userId], FromUtc = open.StartUtc.AddDays(-1), ToUtc = open.StartUtc.AddSeconds(1) }, cancellationToken);
        var start = open.StartUtc;
        TimeEntry before = null;

        for (var step = 0; step < StepsBack; step++)
        {
            var previous = entries.LastOrDefault(entry => entry.ItemId != open.ItemId && entry.EndUtc == start);

            if (previous is null)
            {
                break;
            }

            if (previous.Source != TimeTrackerConstants.Sources.Ticket)
            {
                before = previous;

                break;
            }

            start = previous.StartUtc;
        }

        return before is null
            ? await _tracker.ClockOutAsync(userId, actor, cancellationToken)
            : await _tracker.SwitchAsync(userId, userName, before.StageId, before.Source, actor, before.SourceRef, cancellationToken);
    }

    /// <summary>
    /// The time worked on a ticket, by person, and whether a user is working on it now.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="userId">The user asking.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket's time.</returns>
    public async Task<TicketTimeSummary> SummarizeAsync(string ticketId, string userId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var entries = await _tracker.ListAsync(new TimeEntryQuery { SourceRef = SourceRefOf(ticketId) }, cancellationToken);
        var people = entries
            .GroupBy(entry => entry.UserId, StringComparer.Ordinal)
            .Select(group => new TicketTimePerson(
                group.Key,
                group.Select(entry => entry.UserName).LastOrDefault(name => !string.IsNullOrEmpty(name)) ?? group.Key,
                (int)Math.Round(group.Sum(entry => ((entry.EndUtc ?? now) - entry.StartUtc).TotalMinutes))))
            .OrderByDescending(person => person.Minutes)
            .ToList();
        var mine = entries.FirstOrDefault(entry => entry.UserId == userId && entry.EndUtc is null);

        return new TicketTimeSummary(people, people.Sum(person => person.Minutes), mine?.StartUtc);
    }
}

/// <summary>
/// The time worked on a ticket.
/// </summary>
/// <param name="People">Each person's minutes, most first.</param>
/// <param name="TotalMinutes">All of it.</param>
/// <param name="WorkingSinceUtc">When the user asking started working on it, while they are.</param>
public sealed record TicketTimeSummary(IReadOnlyList<TicketTimePerson> People, int TotalMinutes, DateTime? WorkingSinceUtc);

/// <summary>
/// One person's time on a ticket.
/// </summary>
/// <param name="UserId">The user.</param>
/// <param name="Name">Their name.</param>
/// <param name="Minutes">Their minutes.</param>
public sealed record TicketTimePerson(string UserId, string Name, int Minutes);
