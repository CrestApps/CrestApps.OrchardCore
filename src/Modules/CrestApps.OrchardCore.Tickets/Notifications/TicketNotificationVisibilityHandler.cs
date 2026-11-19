using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Notifications;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// Hides a ticket notification from a user who may no longer see the ticket, such as after it moved to a desk they don't
/// work on, or once it is deleted. An approver keeps the notice of a step they decide, which they may open without
/// seeing the ticket otherwise, and a requester keeps the notices about their own request.
/// </summary>
internal sealed class TicketNotificationVisibilityHandler : INotificationVisibilityHandler
{
    private static readonly HashSet<string> _types = new(StringComparer.Ordinal)
    {
        TicketsConstants.NotificationTypes.Assigned,
        TicketsConstants.NotificationTypes.TeamAssigned,
        TicketsConstants.NotificationTypes.RequesterReplied,
        TicketsConstants.NotificationTypes.NoteAdded,
        TicketsConstants.NotificationTypes.StatusChanged,
        TicketsConstants.NotificationTypes.ApprovalRequested,
        TicketsConstants.NotificationTypes.ServiceLevel,
        TicketsConstants.NotificationTypes.Merged,
        TicketsConstants.NotificationTypes.Replied,
        TicketsConstants.NotificationTypes.RequestReceived,
        TicketsConstants.NotificationTypes.RequestSolved,
        TicketsConstants.NotificationTypes.RatingRequested,
        TicketsConstants.NotificationTypes.Workflow,
    };

    private readonly ISession _session;
    private readonly ITicketAccessService _access;

    public TicketNotificationVisibilityHandler(ISession session, ITicketAccessService access)
    {
        _session = session;
        _access = access;
    }

    /// <summary>
    /// Gets whether a notification type is one of the ticket notifications.
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <returns><see langword="true"/> for a ticket notification.</returns>
    public static bool IsTicketType(string type)
        => type is not null && _types.Contains(type);

    /// <inheritdoc/>
    public async Task EvaluateAsync(IReadOnlyList<NotificationVisibilityContext> contexts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        var ours = contexts.Where(context => IsTicketType(context.Notification.Type) && !string.IsNullOrEmpty(context.Notification.ReferenceId)).ToList();

        if (ours.Count == 0)
        {
            return;
        }

        var ids = ours.Select(context => context.Notification.ReferenceId).Distinct(StringComparer.Ordinal).ToArray();
        var tickets = (await _session.Query<ContentItem, TicketIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken))
            .ToDictionary(ticket => ticket.ContentItemId, StringComparer.Ordinal);
        var allowed = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var context in ours)
        {
            if (!tickets.TryGetValue(context.Notification.ReferenceId, out var ticket))
            {
                context.Hide();

                continue;
            }

            if (KeptWithoutViewing(context, ticket))
            {
                continue;
            }

            if (!allowed.TryGetValue(ticket.ContentItemId, out var canView))
            {
                canView = await _access.CanAsync(context.User, ticket, TicketOperation.ViewAssigned, cancellationToken);
                allowed[ticket.ContentItemId] = canView;
            }

            if (!canView)
            {
                context.Hide();
            }
        }
    }

    // An approver of the step the ticket waits for, and the requester of their own request, keep their notices.
    private static bool KeptWithoutViewing(NotificationVisibilityContext context, ContentItem ticket)
    {
        var userId = context.Notification.UserId;
        var part = ticket.GetOrCreate<TicketPart>();

        return context.Notification.Type switch
        {
            TicketsConstants.NotificationTypes.ApprovalRequested => part.Approval is { State: TicketApprovalState.Pending } approval
                && approval.CurrentStep >= 0
                && approval.CurrentStep < approval.Steps.Count
                && approval.Steps[approval.CurrentStep].ApproverIds.Contains(userId, StringComparer.Ordinal),
            TicketsConstants.NotificationTypes.Replied
                or TicketsConstants.NotificationTypes.RequestReceived
                or TicketsConstants.NotificationTypes.RequestSolved
                or TicketsConstants.NotificationTypes.RatingRequested => part.Requester == new PartyRef(PartyKinds.User, userId),
            _ => false,
        };
    }
}
