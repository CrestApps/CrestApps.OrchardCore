using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// Maps every content item with the ticket part to <see cref="TicketIndex"/>. Ticket types are neither draftable nor
/// versionable, so only the latest version is indexed.
/// </summary>
public sealed class TicketIndexProvider : IndexProvider<ContentItem>
{
    /// <summary>
    /// The longest subject kept in the index.
    /// </summary>
    public const int SubjectLength = 255;

    public const int EmailLength = 254;

    /// <summary>
    /// The due time kept for a ticket with nothing due, so it sorts after every ticket that has one.
    /// </summary>
    public static readonly DateTime NoDueUtc = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<TicketIndex>()
            .When(item => item.Latest && item.Has<TicketPart>())
            .Map(item =>
            {
                item.TryGet<TicketPart>(out var part);

                return new TicketIndex
                {
                    ContentItemId = item.ContentItemId,
                    ContentType = item.ContentType,
                    Number = part.Number,
                    Subject = item.DisplayText?.Length > SubjectLength ? item.DisplayText[..SubjectLength] : item.DisplayText,
                    DeskId = part.DeskId,
                    StatusId = part.StatusId,
                    StatusCategory = part.StatusCategory,
                    PriorityId = part.PriorityId,
                    PriorityRank = part.PriorityRank,
                    TeamId = part.TeamId,
                    AssigneeId = part.AssigneeId,
                    Requester = part.Requester.IsEmpty ? null : part.Requester.ToString(),
                    RequesterEmail = NormalizeEmail(part.RequesterContact?.Email),
                    AccountId = part.AccountId,
                    Channel = part.Channel,
                    MergedIntoId = part.MergedIntoId,
                    CreatedUtc = item.CreatedUtc ?? DateTime.MinValue,
                    ModifiedUtc = item.ModifiedUtc ?? item.CreatedUtc ?? DateTime.MinValue,
                    NextDueUtc = part.NextDueUtc,
                    DueSortUtc = part.NextDueUtc ?? NoDueUtc,
                    SolvedUtc = part.SolvedUtc,
                    AssignedUtc = part.AssignedUtc,
                    ClosedUtc = part.ClosedUtc,
                    FirstResponseUtc = part.FirstResponseUtc,
                    StatusChangedUtc = part.StatusChangedUtc,
                    LastCustomerActivityUtc = part.LastCustomerActivityUtc,
                    LastAgentActivityUtc = part.LastAgentActivityUtc,
                    ReplyCount = part.ReplyCount,
                    AgentReplyCount = part.AgentReplyCount,
                    ReopenCount = part.ReopenCount,
                    EscalationLevel = part.EscalationLevel,
                };
            });

    /// <summary>
    /// Gets an e-mail as the index keeps it.
    /// </summary>
    /// <param name="email">The e-mail.</param>
    /// <returns>The e-mail trimmed and in lower case, or <see langword="null"/> when it is empty or too long.</returns>
    public static string NormalizeEmail(string email)
        => email?.Trim().ToLowerInvariant() is { Length: > 0 and <= EmailLength } normalized ? normalized : null;
}
