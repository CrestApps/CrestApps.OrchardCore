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
                    AccountId = part.AccountId,
                    Channel = part.Channel,
                    MergedIntoId = part.MergedIntoId,
                    CreatedUtc = item.CreatedUtc ?? DateTime.MinValue,
                    ModifiedUtc = item.ModifiedUtc ?? item.CreatedUtc ?? DateTime.MinValue,
                    NextDueUtc = part.NextDueUtc,
                    SolvedUtc = part.SolvedUtc,
                    AssignedUtc = part.AssignedUtc,
                    ClosedUtc = part.ClosedUtc,
                };
            });
}
