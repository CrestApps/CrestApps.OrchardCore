using CrestApps.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Keeps tickets' protected items in documents of their own, each value encrypted with the tenant's Data Protection keys
/// under a purpose of its own, and notes every change and reveal on the ticket's timeline with the labels only.
/// </summary>
public sealed class DefaultTicketProtectedInformation : ITicketProtectedInformation
{
    /// <summary>
    /// The Data Protection purpose of the values. Changing it makes every stored value unreadable.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.OrchardCore.Tickets.ProtectedInformation";

    private readonly ISession _session;
    private readonly IDataProtector _protector;
    private readonly ITimelineService _timeline;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTicketProtectedInformation"/> class.
    /// </summary>
    public DefaultTicketProtectedInformation(
        ISession session,
        IDataProtectionProvider dataProtectionProvider,
        ITimelineService timeline,
        IClock clock,
        IStringLocalizer<DefaultTicketProtectedInformation> stringLocalizer)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        _session = session;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _timeline = timeline;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketProtectedItem>> ListAsync(string ticketId, CancellationToken cancellationToken = default)
        => [.. (await DocumentsAsync(ticketId, cancellationToken)).Select(Item)];

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> FindTicketsWithItemsAsync(IEnumerable<string> ticketIds, bool requesterSharedOnly = false, CancellationToken cancellationToken = default)
    {
        var ids = (ticketIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        // The index holds ids only; who shared an item is on its document (a page of tickets has few).
        if (requesterSharedOnly)
        {
            var documents = await _session.Query<TicketProtectedItemDocument, TicketProtectedItemIndex>(index => index.TicketId.IsIn(ids)).ListAsync(cancellationToken);

            return documents.Where(document => document.FromRequester).Select(document => document.TicketId).ToHashSet(StringComparer.Ordinal);
        }

        var rows = await _session.QueryIndex<TicketProtectedItemIndex>(index => index.TicketId.IsIn(ids)).ListAsync(cancellationToken);

        return rows.Select(row => row.TicketId).ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<TicketProtectedItem> AddAsync(string ticketId, string label, string value, TicketActor actor, bool fromRequester = false, CancellationToken cancellationToken = default)
    {
        label = label?.Trim();

        if (string.IsNullOrEmpty(ticketId)
            || string.IsNullOrEmpty(label) || label.Length > ITicketProtectedInformation.MaxLabelLength
            || string.IsNullOrWhiteSpace(value) || value.Length > ITicketProtectedInformation.MaxValueLength
            || await _session.QueryIndex<TicketProtectedItemIndex>(index => index.TicketId == ticketId).CountAsync(cancellationToken) >= ITicketProtectedInformation.MaxItemsPerTicket)
        {
            return null;
        }

        var document = new TicketProtectedItemDocument
        {
            ItemId = UniqueId.GenerateId(),
            TicketId = ticketId,
            Label = label,
            ProtectedValue = _protector.Protect(value),
            CreatedUtc = _clock.UtcNow,
            CreatedBy = actor?.Party ?? default,
            CreatedByName = actor?.Name,
            FromRequester = fromRequester,
        };

        await _session.SaveAsync(document, cancellationToken: cancellationToken);
        await NoteAsync(ticketId, actor, fromRequester
            ? S["The requester added protected information: {0}.", label]
            : S["Protected information was added: {0}.", label], cancellationToken);

        return Item(document);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string ticketId, string itemId, TicketActor actor, bool requesterSharedOnly = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(itemId)
            || await _session.Query<TicketProtectedItemDocument, TicketProtectedItemIndex>(index => index.TicketId == ticketId && index.ItemId == itemId).FirstOrDefaultAsync(cancellationToken) is not { } document
            || requesterSharedOnly && !document.FromRequester)
        {
            return false;
        }

        _session.Delete(document);
        await NoteAsync(ticketId, actor, S["Protected information was removed: {0}.", document.Label], cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketProtectedValue>> ReadValuesAsync(string ticketId, bool requesterSharedOnly = false, CancellationToken cancellationToken = default)
        => [.. (await DocumentsAsync(ticketId, cancellationToken))
            .Where(document => !requesterSharedOnly || document.FromRequester)
            .Select(document => new TicketProtectedValue(document.ItemId, document.Label, Unprotect(document.ProtectedValue)))];

    /// <inheritdoc/>
    public async Task RecordRevealAsync(string ticketId, TicketActor actor, bool succeeded, bool requesterSharedOnly = false, CancellationToken cancellationToken = default)
    {
        var labels = string.Join(", ", (await DocumentsAsync(ticketId, cancellationToken))
            .Where(document => !requesterSharedOnly || document.FromRequester)
            .Select(document => document.Label));

        await NoteAsync(ticketId, actor, succeeded
            ? S["The protected information was shown: {0}.", labels]
            : S["A wrong password was given to show the protected information."], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> MoveAsync(string fromTicketId, string toTicketId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fromTicketId) || string.IsNullOrEmpty(toTicketId) || fromTicketId == toTicketId)
        {
            return 0;
        }

        var documents = await DocumentsAsync(fromTicketId, cancellationToken);

        foreach (var document in documents)
        {
            document.TicketId = toTicketId;
            await _session.SaveAsync(document, cancellationToken: cancellationToken);
        }

        return documents.Count;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAllAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        var documents = await DocumentsAsync(ticketId, cancellationToken);

        foreach (var document in documents)
        {
            _session.Delete(document);
        }

        return documents.Count;
    }

    private async Task<IReadOnlyList<TicketProtectedItemDocument>> DocumentsAsync(string ticketId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(ticketId)
            ? []
            : [.. (await _session.Query<TicketProtectedItemDocument, TicketProtectedItemIndex>(index => index.TicketId == ticketId).ListAsync(cancellationToken)).OrderBy(document => document.CreatedUtc).ThenBy(document => document.Id)];

    // A value its keys can no longer read (keys lost or rotated out) shows as unreadable rather than failing the page.
    private string Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return S["(This value can no longer be read.)"];
        }
    }

    private Task<TimelineResult> NoteAsync(string ticketId, TicketActor actor, string body, CancellationToken cancellationToken)
        => _timeline.AddAsync(new TimelineEntry
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticketId),
            Kind = TicketsConstants.EntryKinds.Event,
            Visibility = TimelineVisibility.Internal,
            Author = actor?.Party ?? default,
            AuthorName = actor?.Name,
            Body = body,
        }, cancellationToken);

    private static TicketProtectedItem Item(TicketProtectedItemDocument document)
        => new(document.ItemId, document.TicketId, document.Label, document.CreatedUtc, document.CreatedByName, document.FromRequester);
}
