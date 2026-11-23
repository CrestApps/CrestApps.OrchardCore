using CrestApps.OrchardCore.MailingLists.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.MailingLists.Core.Indexes;

/// <summary>
/// Maps mailing list members to their index.
/// </summary>
public sealed class MailingListMemberIndexProvider : IndexProvider<MailingListMember>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberIndexProvider"/> class.
    /// </summary>
    public MailingListMemberIndexProvider()
    {
        CollectionName = MailingListsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MailingListMember> context)
        => context.For<MailingListMemberIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(member => new MailingListMemberIndex
            {
                ItemId = member.ItemId,
                ListId = member.ListId,
                NormalizedEmail = MailingListsIndexSchema.Cut(member.NormalizedEmail, MailingListsIndexSchema.EmailLength),
                Status = member.Status,
                UserId = member.UserId,
                SubscribedUtc = member.SubscribedUtc,
                StatusChangedUtc = member.StatusChangedUtc,
                Source = MailingListsIndexSchema.Cut(member.Source, MailingListsIndexSchema.SourceLength),
                SyncState = member.SyncState,
            });
}
