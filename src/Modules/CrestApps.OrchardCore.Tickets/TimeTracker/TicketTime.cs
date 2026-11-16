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
        => (await SettingsAsync(ticket))?.StageId;

    /// <summary>
    /// Logs time a user spent on a ticket after the fact. The entry takes its place in the user's day: the time it
    /// covers (their work, say) is cut around it, so their total doesn't change.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="log">Who, how long, when, what and whether it is billed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entry, or why it wasn't logged.</returns>
    public async Task<TimeTrackerResult> LogAsync(ContentItem ticket, TicketTimeLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(log);

        if (await SettingsAsync(ticket) is not { } settings)
        {
            return TimeTrackerResult.Fail(S["This ticket's desk doesn't record time."]);
        }

        if (log.Minutes < 1 || log.Minutes > TicketTimeLog.MaxMinutes)
        {
            return TimeTrackerResult.Fail(S["Enter the time spent, from a minute to 24 hours."]);
        }

        var start = log.StartUtc ?? _clock.UtcNow.AddMinutes(-log.Minutes);
        var number = ticket.GetOrCreate<TicketPart>().Number;

        return await _tracker.InsertAsync(
            new TimeEntry
            {
                UserId = log.UserId,
                UserName = log.UserName,
                StageId = !log.Billable && !string.IsNullOrEmpty(settings.NotBilledStageId) ? settings.NotBilledStageId : settings.StageId,
                StartUtc = start,
                EndUtc = start.AddMinutes(log.Minutes),
                Source = TimeTrackerConstants.Sources.Ticket,
                SourceRef = SourceRefOf(ticket.ContentItemId),
                Note = string.IsNullOrWhiteSpace(log.Note) ? null : log.Note.Trim(),
            },
            log.Actor,
            string.IsNullOrEmpty(number) ? S["Time logged on a ticket"].Value : S["Time logged on ticket {0}", number].Value,
            cancellationToken);
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
    public Task<TicketTimeSummary> SummarizeAsync(string ticketId, string userId, CancellationToken cancellationToken = default)
        => SummarizeAsync(ticketId, null, userId, cancellationToken);

    /// <summary>
    /// The time worked on a ticket, by person, with its entries and what isn't billed, and whether a user is working on
    /// it now.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="userId">The user asking.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket's time.</returns>
    public async Task<TicketTimeSummary> SummarizeAsync(ContentItem ticket, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return await SummarizeAsync(ticket.ContentItemId, await SettingsAsync(ticket), userId, cancellationToken);
    }

    private async Task<TicketTimeSummary> SummarizeAsync(string ticketId, TicketDeskTimeSettings settings, string userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var entries = await _tracker.ListAsync(new TimeEntryQuery { SourceRef = SourceRefOf(ticketId) }, cancellationToken);
        var notBilledStageId = string.IsNullOrEmpty(settings?.NotBilledStageId) ? null : settings.NotBilledStageId;

        int MinutesOf(TimeEntry entry) => (int)Math.Round(((entry.EndUtc ?? now) - entry.StartUtc).TotalMinutes);

        var people = entries
            .GroupBy(entry => entry.UserId, StringComparer.Ordinal)
            .Select(group => new TicketTimePerson(
                group.Key,
                group.Select(entry => entry.UserName).LastOrDefault(name => !string.IsNullOrEmpty(name)) ?? group.Key,
                group.Sum(MinutesOf)))
            .OrderByDescending(person => person.Minutes)
            .ToList();
        var mine = entries.FirstOrDefault(entry => entry.UserId == userId && entry.EndUtc is null);

        return new TicketTimeSummary(people, people.Sum(person => person.Minutes), mine?.StartUtc)
        {
            Entries = [.. entries
                .OrderByDescending(entry => entry.StartUtc)
                .Select(entry => new TicketTimeEntry(
                    string.IsNullOrEmpty(entry.UserName) ? entry.UserId : entry.UserName,
                    entry.StartUtc,
                    entry.EndUtc,
                    MinutesOf(entry),
                    entry.Note,
                    entry.StageId != notBilledStageId))],
            NotBilledMinutes = notBilledStageId is null ? 0 : entries.Where(entry => entry.StageId == notBilledStageId).Sum(MinutesOf),
            CanChooseBillable = notBilledStageId is not null,
        };
    }

    // The ticket desk's time settings, when it records time.
    private async Task<TicketDeskTimeSettings> SettingsAsync(ContentItem ticket)
    {
        var deskId = ticket?.GetOrCreate<TicketPart>().DeskId;

        return string.IsNullOrEmpty(deskId)
            || await _desks.FindByIdAsync(deskId) is not { } desk
            || !desk.TryGet<TicketDeskTimeSettings>(out var settings)
            || string.IsNullOrEmpty(settings.StageId)
            ? null
            : settings;
    }
}
