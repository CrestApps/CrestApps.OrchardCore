using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Who may read a ticket's timeline: whoever may view the ticket reads its replies, notes and events. Nothing is added
/// through the generic timeline composer: replies and notes go through <see cref="ITicketService"/>, which keeps the
/// ticket's counters, service levels and life cycle. The ticket is read through the content manager, because the
/// ticket service depends on the timeline, whose access providers this is one of.
/// </summary>
public sealed class TicketTimelineAccess : ITimelineAccessProvider
{
    // Ids per index query, below every database's parameter limit.
    private const int BatchSize = 500;

    private readonly IContentManager _contentManager;
    private readonly ITicketAccessService _access;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTimelineAccess"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="access">The ticket access service.</param>
    /// <param name="session">The session, for the ticket index.</param>
    public TicketTimelineAccess(IContentManager contentManager, ITicketAccessService access, ISession session)
    {
        _contentManager = contentManager;
        _access = access;
        _session = session;
    }

    /// <inheritdoc/>
    public string SubjectKind => TicketsConstants.RecordKind;

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        if (subject.Kind != TicketsConstants.RecordKind || string.IsNullOrEmpty(subject.Id))
        {
            return false;
        }

        var ticket = await _contentManager.GetAsync(subject.Id, VersionOptions.Latest);

        if (ticket?.TryGet<TicketPart>(out var part) != true)
        {
            return false;
        }

        // An agent who may see the ticket reads all of it, internal notes included.
        if (await _access.CanAsync(user, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return true;
        }

        // The requester reads the public entries of their own ticket, never an internal note.
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return visibility == TimelineVisibility.Public
            && !string.IsNullOrEmpty(userId)
            && part.Requester == new PartyRef(PartyKinds.User, userId);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The same rule as <see cref="CanViewAsync"/>, answered from the ticket index: the reader's ticket scope
    /// (<see cref="TicketScopeFilter"/>) for every entry, and the requester's own tickets for public entries.
    /// </remarks>
    public async Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        var visible = new HashSet<string>(StringComparer.Ordinal);
        var ids = (subjectIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (user is null || ids.Length == 0)
        {
            return visible;
        }

        var scope = await _access.ApplyScopeAsync(user, new TicketQuery(), cancellationToken);
        var predicate = scope is null ? null : TicketScopeFilter.ToPredicate(scope);
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var requester = visibility == TimelineVisibility.Public && !string.IsNullOrEmpty(userId) ? new PartyRef(PartyKinds.User, userId).ToString() : null;

        foreach (var batch in ids.Chunk(BatchSize))
        {
            if (predicate is not null)
            {
                visible.UnionWith((await _session.QueryIndex(predicate).Where(index => index.ContentItemId.IsIn(batch)).ListAsync(cancellationToken)).Select(index => index.ContentItemId));
            }

            if (requester is not null)
            {
                visible.UnionWith((await _session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(batch) && index.Requester == requester).ListAsync(cancellationToken)).Select(index => index.ContentItemId));
            }
        }

        return visible;
    }

    /// <inheritdoc/>
    public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
