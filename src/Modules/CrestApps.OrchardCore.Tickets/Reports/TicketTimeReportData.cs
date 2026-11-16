using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// Reads the time logged on tickets (Time Tracker entries with the <see cref="TimeTrackerConstants.Sources.Ticket"/>
/// source) for the ticket time reports, keeping only the tickets in the reader's ticket scope.
/// </summary>
internal sealed class TicketTimeReportData
{
    // How many tickets' entries are read at a time.
    private const int IdBatchSize = 500;

    private static readonly string _sourceRefPrefix = TicketTime.SourceRefOf(string.Empty);

    private readonly TicketReportData _data;
    private readonly ITimeTrackerService _tracker;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public TicketTimeReportData(
        TicketReportData data,
        ITimeTrackerService tracker,
        ICatalog<TicketDesk> desks,
        IContentDefinitionManager contentDefinitionManager)
    {
        _data = data;
        _tracker = tracker;
        _desks = desks;
        _contentDefinitionManager = contentDefinitionManager;
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
        var tickets = await _data.ReadByIdsAsync(scope, entries.Select(TicketIdOf), cancellationToken);
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
    /// Every entry on the given tickets, by ticket, whenever it was logged (time logged before a ticket was opened
    /// counts too), a batch of tickets at a time.
    /// </summary>
    public async Task<ILookup<string, TimeEntry>> ReadEntriesAsync(IReadOnlyCollection<string> ticketIds, CancellationToken cancellationToken)
    {
        var entries = new List<TimeEntry>();

        foreach (var batch in ticketIds.Distinct(StringComparer.Ordinal).Chunk(IdBatchSize))
        {
            entries.AddRange(await _tracker.ListAsync(new TimeEntryQuery
            {
                Source = TimeTrackerConstants.Sources.Ticket,
                SourceRefs = [.. batch.Select(TicketTime.SourceRefOf)],
            }, cancellationToken));
        }

        return entries.ToLookup(entry => TicketIdOf(entry) ?? string.Empty, StringComparer.Ordinal);
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

    private async Task<HashSet<string>> NotBilledStagesAsync(CancellationToken cancellationToken)
        => [.. (await _desks.GetAllAsync(cancellationToken))
            .Select(desk => desk.TryGet<TicketDeskTimeSettings>(out var settings) ? settings.NotBilledStageId : null)
            .Where(stageId => !string.IsNullOrEmpty(stageId))];
}
