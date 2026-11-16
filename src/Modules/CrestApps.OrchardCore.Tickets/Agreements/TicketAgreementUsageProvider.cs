using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// Counts the billable time logged on an agreement's tickets against its hours: the time in a desk's ticket stage (not
/// its stage for time not billed) on the tickets that work under the agreement, unless a ticket is marked not billable
/// or no charge.
/// </summary>
internal sealed class TicketAgreementUsageProvider : IAgreementUsageProvider
{
    // How many tickets' entries are read at a time.
    private const int BatchSize = 500;

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

        var agreementId = agreement.ItemId;
        var tickets = (await _session.Query<ContentItem, TicketIndex>(index => index.AgreementId == agreementId).ListAsync(cancellationToken))
            .Select(ticket => (Ticket: ticket, Part: ticket.GetOrCreate<TicketPart>()))
            .Where(item => item.Part.BillingType is not (TicketBillingType.NotBillable or TicketBillingType.NoCharge))
            .ToDictionary(item => TicketTime.SourceRefOf(item.Ticket.ContentItemId), StringComparer.Ordinal);

        if (tickets.Count == 0)
        {
            return [];
        }

        var notBilled = (await _desks.GetAllAsync(cancellationToken))
            .Select(desk => desk.TryGet<TicketDeskTimeSettings>(out var settings) ? settings.NotBilledStageId : null)
            .Where(stageId => !string.IsNullOrEmpty(stageId))
            .ToHashSet(StringComparer.Ordinal);
        var now = _clock.UtcNow;
        var usage = new List<AgreementUsage>();

        foreach (var batch in tickets.Keys.Chunk(BatchSize))
        {
            var entries = await _tracker.ListAsync(new TimeEntryQuery { Source = TimeTrackerConstants.Sources.Ticket, SourceRefs = batch, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);

            foreach (var entry in entries.Where(entry => entry.StartUtc >= fromUtc && entry.StartUtc < toUtc && !notBilled.Contains(entry.StageId ?? string.Empty)))
            {
                var (ticket, part) = tickets[entry.SourceRef];

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
        }

        return usage;
    }
}
