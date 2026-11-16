using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// Tells agents what happens on their tickets, each kind as its own notification type so they choose how to get it:
/// a ticket given to their team, a reply from the requester, an internal note, a status someone else set, a service-level
/// target close or missed, and a ticket merged into theirs. A requester who signs in also hears of an agent's reply in the
/// notification center. Nobody is told about their own change, a change already told another way (a mention, the
/// requester's reply that reopens the ticket) or the same unread service-level notice twice.
/// </summary>
internal sealed class TicketNotificationObserver : ITicketObserver
{
    private readonly TicketNotificationSender _sender;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly UserManager<IUser> _userManager;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public TicketNotificationObserver(
        TicketNotificationSender sender,
        ICatalog<TicketDesk> desks,
        UserManager<IUser> userManager,
        IServiceProvider services,
        IStringLocalizer<TicketNotificationObserver> stringLocalizer)
    {
        _sender = sender;
        _desks = desks;
        _userManager = userManager;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        if (!string.IsNullOrEmpty(part.TeamId))
        {
            await TellTeamAsync(ticket, part, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        if (changes?.Any(change => change.Key == TicketChangeKeys.Team && !string.IsNullOrEmpty(change.ToValue)) == true
            && !string.IsNullOrEmpty(part.TeamId))
        {
            await TellTeamAsync(ticket, part, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task RepliedAsync(ContentItem ticket, TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        if (string.Equals(entry?.Kind, TicketsConstants.EntryKinds.Note, StringComparison.Ordinal))
        {
            // A note that mentions the assignee already told them, as a mention.
            if (!string.IsNullOrEmpty(part.AssigneeId) && !await MentionsAsync(entry.Body, part.AssigneeId))
            {
                await _sender.SendAsync([part.AssigneeId], ticket, TicketsConstants.NotificationTypes.NoteAdded, title => S["A note was added to your ticket: {0}", title], cancellationToken: cancellationToken);
            }

            return;
        }

        if (fromRequester)
        {
            await _sender.SendAsync(
                await AssigneeOrTeamAsync(part, cancellationToken),
                ticket,
                TicketsConstants.NotificationTypes.RequesterReplied,
                title => S["The requester replied: {0}", title],
                cancellationToken: cancellationToken);

            return;
        }

        // A requester who signs in hears of an agent's public reply in the notification center too (the e-mail goes on).
        if (entry?.Visibility == TimelineVisibility.Public
            && part.Requester.Kind == PartyKinds.User
            && !string.IsNullOrEmpty(part.Requester.Id)
            && !string.IsNullOrEmpty(part.Number))
        {
            await _sender.SendAsync(
                [part.Requester.Id],
                ticket,
                TicketsConstants.NotificationTypes.Replied,
                title => S["Your request has a new reply: {0}", title],
                url: "~/account/requests/" + Uri.EscapeDataString(part.Number),
                cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        // A new ticket's first status is no change; the requester's reply that reopens a ticket is told as the reply.
        if (previous is null || current is null || string.IsNullOrEmpty(part.AssigneeId) || IsRequester(part, TicketEventActor.Current))
        {
            return;
        }

        await _sender.SendAsync(
            [part.AssigneeId],
            ticket,
            TicketsConstants.NotificationTypes.StatusChanged,
            title => S["Your ticket is now {0}: {1}", current.Name, title],
            info: info => info.Status = current.Name,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ServiceLevelAsync(ContentItem ticket, string metric, bool breached, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        await _sender.SendAsync(
            await AssigneeOrTeamAsync(part, cancellationToken),
            ticket,
            TicketsConstants.NotificationTypes.ServiceLevel,
            title => breached ? S["A service-level target was missed: {0}", title] : S["A service-level target is due soon: {0}", title],
            info: info =>
            {
                info.Metric = metric;
                info.Breached = breached;
            },
            skip: notification => notification.TryGet<TicketNotificationInfo>(out var sent) && sent.Metric == metric && sent.Breached == breached,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default)
    {
        if (into?.GetOrCreate<TicketPart>() is not { AssigneeId.Length: > 0 } target)
        {
            return;
        }

        var number = ticket.GetOrCreate<TicketPart>().Number;

        await _sender.SendAsync(
            [target.AssigneeId],
            into,
            TicketsConstants.NotificationTypes.Merged,
            title => S["Ticket {0} was merged into yours: {1}", number, title],
            info: info => info.MergedNumber = number,
            cancellationToken: cancellationToken);
    }

    // The team's members other than the assignee, who hears of the ticket as theirs, on a desk that tells assignees.
    private async Task TellTeamAsync(ContentItem ticket, TicketPart part, CancellationToken cancellationToken)
    {
        if (await _desks.FindByIdAsync(part.DeskId, cancellationToken) is not { NotifyAssignee: true })
        {
            return;
        }

        var members = (await TeamMembersAsync(part.TeamId, cancellationToken))
            .Where(userId => !string.Equals(userId, part.AssigneeId, StringComparison.Ordinal))
            .ToList();

        if (members.Count > 0)
        {
            await _sender.SendAsync(members, ticket, TicketsConstants.NotificationTypes.TeamAssigned, title => S["A ticket was given to your team: {0}", title], cancellationToken: cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> AssigneeOrTeamAsync(TicketPart part, CancellationToken cancellationToken)
        => !string.IsNullOrEmpty(part.AssigneeId)
            ? [part.AssigneeId]
            : await TeamMembersAsync(part.TeamId, cancellationToken);

    private async Task<IReadOnlyList<string>> TeamMembersAsync(string teamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(teamId) || _services.GetService<ITeamMembershipService>() is not { } memberships)
        {
            return [];
        }

        return [.. (await memberships.GetMembersAsync(teamId, cancellationToken: cancellationToken))
            .Select(member => member.UserId)
            .Where(userId => !string.IsNullOrEmpty(userId))
            .Distinct(StringComparer.Ordinal)];
    }

    private async Task<bool> MentionsAsync(string body, string userId)
    {
        if (string.IsNullOrEmpty(body) || !body.Contains('@', StringComparison.Ordinal) || await _userManager.FindByIdAsync(userId) is not { } user)
        {
            return false;
        }

        // The rule mentions are told by, so a note told as a mention isn't told again, and only then.
        return TimelineMentions.Mentions(body, user.UserName);
    }

    private static bool IsRequester(TicketPart part, TicketActor actor)
        => actor is not null && !actor.Party.IsEmpty && actor.Party == part.Requester;
}
