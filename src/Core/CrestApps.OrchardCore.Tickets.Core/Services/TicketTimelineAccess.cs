using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Who may read a ticket's timeline: whoever may view the ticket reads its replies, notes and events. Nothing is added
/// through the generic timeline composer: replies and notes go through <see cref="ITicketService"/>, which keeps the
/// ticket's counters, service levels and life cycle. The ticket is read through the content manager, because the
/// ticket service depends on the timeline, whose access providers this is one of.
/// </summary>
public sealed class TicketTimelineAccess : ITimelineAccessProvider
{
    private readonly IContentManager _contentManager;
    private readonly ITicketAccessService _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTimelineAccess"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="access">The ticket access service.</param>
    public TicketTimelineAccess(IContentManager contentManager, ITicketAccessService access)
    {
        _contentManager = contentManager;
        _access = access;
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

        // Any view scope lets the user see the ticket; the access service checks the ticket is in that scope.
        return ticket?.Has<TicketPart>() == true && await _access.CanAsync(user, ticket, TicketOperation.ViewAssigned, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
