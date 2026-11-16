using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// Reads the time logged on tickets (Time Tracker entries with the <see cref="TimeTrackerConstants.Sources.Ticket"/>
/// source) for the ticket time reports, keeping only the tickets in the reader's ticket scope.
/// </summary>
internal sealed class TicketTimeReportData
{
    // How many tickets are read by id at a time.
    private const int IdBatchSize = 500;

    private static readonly string _sourceRefPrefix = TicketTime.SourceRefOf(string.Empty);

    private readonly TicketReportData _data;
    private readonly ITimeTrackerService _tracker;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IServiceProvider _services;

    public TicketTimeReportData(
        TicketReportData data,
        ITimeTrackerService tracker,
        ICatalog<TicketDesk> desks,
        IContentDefinitionManager contentDefinitionManager,
        IServiceProvider services)
    {
        _data = data;
        _tracker = tracker;
        _desks = desks;
        _contentDefinitionManager = contentDefinitionManager;
        _services = services;
    }

    /// <summary>
    /// The ticket a time entry is for, or <see langword="null"/>.
    /// </summary>
    public static string TicketIdOf(TimeEntry entry)
        => entry?.SourceRef is { } sourceRef && sourceRef.StartsWith(_sourceRefPrefix, StringComparison.Ordinal) && sourceRef.Length > _sourceRefPrefix.Length
            ? sourceRef[_sourceRefPrefix.Length..]
            : null;

    /// <summary>
    /// The work on the reader's tickets in a period, each span clipped to the period.
    /// </summary>
    public async Task<List<TicketWork>> ReadWorkAsync(TicketScopedQuery scope, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var entries = await _tracker.ListAsync(new TimeEntryQuery { Source = TimeTrackerConstants.Sources.Ticket, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);
        var tickets = await ReadTicketsAsync(scope, entries.Select(TicketIdOf), cancellationToken);
        var notBilled = await NotBilledStagesAsync(cancellationToken);
        var now = _data.UtcNow;
        var work = new List<TicketWork>();

        foreach (var entry in entries)
        {
            if (TicketIdOf(entry) is not { } ticketId || !tickets.TryGetValue(ticketId, out var ticket))
            {
                continue;
            }

            var start = fromUtc is { } from && from > entry.StartUtc ? from : entry.StartUtc;
            var end = entry.EndUtc ?? now;

            if (toUtc is { } to && to < end)
            {
                end = to;
            }

            if (end > start)
            {
                work.Add(new TicketWork(ticket, entry, start, end, !notBilled.Contains(entry.StageId ?? string.Empty)));
            }
        }

        return work;
    }

    /// <summary>
    /// Every entry on the given tickets, by ticket, whenever it was logged from a start on.
    /// </summary>
    public async Task<ILookup<string, TimeEntry>> ReadEntriesAsync(IReadOnlyCollection<string> ticketIds, DateTime fromUtc, CancellationToken cancellationToken)
    {
        if (ticketIds.Count == 0)
        {
            return Array.Empty<TimeEntry>().ToLookup(entry => string.Empty, StringComparer.Ordinal);
        }

        var wanted = new HashSet<string>(ticketIds, StringComparer.Ordinal);
        var entries = await _tracker.ListAsync(new TimeEntryQuery { Source = TimeTrackerConstants.Sources.Ticket, FromUtc = fromUtc }, cancellationToken);

        return entries
            .Where(entry => TicketIdOf(entry) is { } id && wanted.Contains(id))
            .ToLookup(entry => TicketIdOf(entry), StringComparer.Ordinal);
    }

    /// <summary>
    /// The names of accounts by id; an account that can't be read keeps its id.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAccountNamesAsync(IEnumerable<string> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Count == 0 || _services.GetService<IContentManager>() is not { } contentManager)
        {
            return names;
        }

        foreach (var account in await contentManager.GetAsync(ids, VersionOptions.Latest))
        {
            if (!string.IsNullOrWhiteSpace(account?.DisplayText))
            {
                names[account.ContentItemId] = account.DisplayText;
            }
        }

        return names;
    }

    /// <summary>
    /// The display names of the ticket types, by content type.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetTypeNamesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync() ?? [])
            .ToDictionary(type => type.Name, type => type.DisplayName, StringComparer.Ordinal);

    /// <summary>
    /// Work time: how long anyone was working, counting time two people worked at once only once.
    /// </summary>
    public static double WorkMinutes(IEnumerable<(DateTime Start, DateTime End)> spans)
    {
        var total = 0d;
        DateTime? start = null;
        DateTime end = default;

        foreach (var span in spans.Where(span => span.End > span.Start).OrderBy(span => span.Start))
        {
            if (start is null || span.Start > end)
            {
                if (start is { } open)
                {
                    total += (end - open).TotalMinutes;
                }

                start = span.Start;
                end = span.End;
            }
            else if (span.End > end)
            {
                end = span.End;
            }
        }

        return start is { } last ? total + (end - last).TotalMinutes : total;
    }

    private async Task<Dictionary<string, TicketFacts>> ReadTicketsAsync(TicketScopedQuery scope, IEnumerable<string> ticketIds, CancellationToken cancellationToken)
    {
        var tickets = new Dictionary<string, TicketFacts>(StringComparer.Ordinal);

        foreach (var batch in ticketIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Chunk(IdBatchSize))
        {
            foreach (var ticket in await _data.ReadAsync(scope, index => index.ContentItemId.IsIn(batch), index => index.CreatedUtc, cancellationToken))
            {
                tickets[ticket.Id] = ticket;
            }
        }

        return tickets;
    }

    private async Task<HashSet<string>> NotBilledStagesAsync(CancellationToken cancellationToken)
        => [.. (await _desks.GetAllAsync(cancellationToken))
            .Select(desk => desk.TryGet<TicketDeskTimeSettings>(out var settings) ? settings.NotBilledStageId : null)
            .Where(stageId => !string.IsNullOrEmpty(stageId))];
}
