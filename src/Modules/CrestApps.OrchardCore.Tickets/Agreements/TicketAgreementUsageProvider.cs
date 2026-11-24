using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// Counts the billable time logged on an agreement's tickets against its hours: the time charged to the agreement (its
/// ticket worked under it when the time was logged, see <see cref="TicketTimeChargeResolver"/>) in a desk's ticket stage
/// (not its stage for time not billed), unless the ticket is marked not billable or no charge. A change of a ticket's
/// agreement leaves the time logged before it where it was.
/// </summary>
internal sealed class TicketAgreementUsageProvider : IAgreementUsageProvider
{
    // How many tickets are read at a time.
    private const int BatchSize = 500;

    private static readonly string _sourceRefPrefix = TicketTime.SourceRefOf(string.Empty);

    private readonly ISession _session;
    private readonly ITimeTrackerService _tracker;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IClock _clock;

    public TicketAgreementUsageProvider(ISession session, ITimeTrackerService tracker, ICatalog<TicketDesk> desks, IClock clock)
    {
        _session = session;
        _tracker = tracker;
        _desks = desks;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "Tickets";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgreementUsage>> ListAsync(Agreement agreement, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var entries = (await _tracker.ListAsync(new TimeEntryQuery
        {
            Source = TimeTrackerConstants.Sources.Ticket,
            ChargedTo = new RecordRef(AgreementsConstants.RecordKind, agreement.ItemId),
            FromUtc = fromUtc,
            ToUtc = toUtc,
        }, cancellationToken)).Where(entry => entry.StartUtc >= fromUtc && entry.StartUtc < toUtc).ToList();

        if (entries.Count == 0)
        {
            return [];
        }

        var notBilled = (await _desks.GetAllAsync(cancellationToken))
            .Select(desk => desk.TryGet<TicketDeskTimeSettings>(out var settings) ? settings.NotBilledStageId : null)
            .Where(stageId => !string.IsNullOrEmpty(stageId))
            .ToHashSet(StringComparer.Ordinal);
        var tickets = new Dictionary<string, (ContentItem Ticket, TicketPart Part)>(StringComparer.Ordinal);
        var ticketIds = entries
            .Select(entry => entry.SourceRef)
            .Where(sourceRef => sourceRef?.StartsWith(_sourceRefPrefix, StringComparison.Ordinal) == true)
            .Select(sourceRef => sourceRef[_sourceRefPrefix.Length..])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var batch in ticketIds.Chunk(BatchSize))
        {
            foreach (var ticket in await _session.Query<ContentItem, TicketIndex>(index => index.ContentItemId.IsIn(batch)).ListAsync(cancellationToken))
            {
                tickets[TicketTime.SourceRefOf(ticket.ContentItemId)] = (ticket, ticket.GetOrCreate<TicketPart>());
            }
        }

        var now = _clock.UtcNow;
        var usage = new List<AgreementUsage>();

        foreach (var entry in entries.Where(entry => !notBilled.Contains(entry.StageId ?? string.Empty)))
        {
            if (!tickets.TryGetValue(entry.SourceRef, out var found) || found.Part.BillingType is TicketBillingType.NotBillable or TicketBillingType.NoCharge)
            {
                continue;
            }

            var (ticket, part) = found;

            usage.Add(new AgreementUsage
            {
                Source = Name,
                SourceRef = entry.SourceRef,
                Description = string.IsNullOrEmpty(part.Number) ? ticket.DisplayText : part.Number + " " + ticket.DisplayText,
                UserId = entry.UserId,
                UserName = entry.UserName,
                StartUtc = entry.StartUtc,
                Minutes = ((entry.EndUtc ?? now) - entry.StartUtc).TotalMinutes,
            });
        }

        return usage;
    }
}
