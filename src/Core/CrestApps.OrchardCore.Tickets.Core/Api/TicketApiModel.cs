using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Api;

/// <summary>
/// A ticket as the API and webhooks give it: its ids and values, never its internal notes.
/// </summary>
public sealed class TicketApiModel
{
    public string Id { get; init; }

    public string Number { get; init; }

    public string Type { get; init; }

    public string Subject { get; init; }

    public string DeskId { get; init; }

    public string StatusId { get; init; }

    public string StatusCategory { get; init; }

    public string PriorityId { get; init; }

    public string TeamId { get; init; }

    public string AssigneeId { get; init; }

    public string Requester { get; init; }

    public string RequesterName { get; init; }

    public string RequesterEmail { get; init; }

    public string AccountId { get; init; }

    public string CategoryId { get; init; }

    /// <summary>
    /// Gets the category and those above it as ids, top first (<c>/top/child/category/</c>).
    /// </summary>
    public string CategoryPath { get; init; }

    public string Channel { get; init; }

    public string MergedIntoId { get; init; }

    public string FollowUpOfId { get; init; }

    public int ReplyCount { get; init; }

    public int ReopenCount { get; init; }

    public int EscalationLevel { get; init; }

    public DateTime? CreatedUtc { get; init; }

    public DateTime? ModifiedUtc { get; init; }

    public DateTime? FirstResponseUtc { get; init; }

    public DateTime? SolvedUtc { get; init; }

    public DateTime? ClosedUtc { get; init; }

    public DateTime? NextDueUtc { get; init; }

    public IReadOnlyList<TicketApiLink> Links { get; init; } = [];

    /// <summary>
    /// Builds the model of a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The model.</returns>
    public static TicketApiModel From(ContentItem ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.TryGet<TicketPart>(out var part);
        part ??= new TicketPart();

        return new TicketApiModel
        {
            Id = ticket.ContentItemId,
            Number = part.Number,
            Type = ticket.ContentType,
            Subject = ticket.DisplayText,
            DeskId = part.DeskId,
            StatusId = part.StatusId,
            StatusCategory = part.StatusCategory.ToString(),
            PriorityId = part.PriorityId,
            TeamId = part.TeamId,
            AssigneeId = part.AssigneeId,
            Requester = part.Requester.IsEmpty ? null : part.Requester.ToString(),
            RequesterName = part.RequesterContact?.Name,
            RequesterEmail = part.RequesterContact?.Email,
            AccountId = part.AccountId,
            CategoryId = part.CategoryId,
            CategoryPath = part.CategoryPath,
            Channel = part.Channel,
            MergedIntoId = part.MergedIntoId,
            FollowUpOfId = part.FollowUpOfId,
            ReplyCount = part.ReplyCount,
            ReopenCount = part.ReopenCount,
            EscalationLevel = part.EscalationLevel,
            CreatedUtc = ticket.CreatedUtc,
            ModifiedUtc = ticket.ModifiedUtc,
            FirstResponseUtc = part.FirstResponseUtc,
            SolvedUtc = part.SolvedUtc,
            ClosedUtc = part.ClosedUtc,
            NextDueUtc = part.NextDueUtc,
            Links = [.. part.Links.Select(link => new TicketApiLink { Record = link.Record.ToString(), Kind = link.Kind })],
        };
    }
}

/// <summary>
/// A link of a ticket, as the API gives it.
/// </summary>
public sealed class TicketApiLink
{
    public string Record { get; init; }

    public string Kind { get; init; }
}
