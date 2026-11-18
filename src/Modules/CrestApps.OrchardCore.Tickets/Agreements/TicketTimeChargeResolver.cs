using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// Charges time logged on a ticket to the agreement the ticket works under when the time is logged, so a later change
/// of the ticket's agreement only moves the time logged after it.
/// </summary>
internal sealed class TicketTimeChargeResolver : ITimeEntryChargeResolver
{
    private static readonly string _sourceRefPrefix = TicketTime.SourceRefOf(string.Empty);

    private readonly ISession _session;

    public TicketTimeChargeResolver(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<RecordRef> ResolveAsync(TimeEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!string.Equals(entry.Source, TimeTrackerConstants.Sources.Ticket, StringComparison.Ordinal)
            || entry.SourceRef?.StartsWith(_sourceRefPrefix, StringComparison.Ordinal) != true)
        {
            return default;
        }

        var ticketId = entry.SourceRef[_sourceRefPrefix.Length..];

        if (await _session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == ticketId).FirstOrDefaultAsync(cancellationToken) is not { } ticket
            || !ticket.TryGet<TicketPart>(out var part)
            || string.IsNullOrEmpty(part.AgreementId))
        {
            return default;
        }

        return new RecordRef(AgreementsConstants.RecordKind, part.AgreementId);
    }
}
