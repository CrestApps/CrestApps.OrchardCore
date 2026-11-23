using CrestApps.OrchardCore.MailingLists.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.MailingLists.Core.Indexes;

/// <summary>
/// Maps mailing lists to their index.
/// </summary>
public sealed class MailingListIndexProvider : IndexProvider<MailingList>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListIndexProvider"/> class.
    /// </summary>
    public MailingListIndexProvider()
    {
        CollectionName = MailingListsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MailingList> context)
        => context.For<MailingListIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(list => new MailingListIndex
            {
                ItemId = list.ItemId,
                Name = MailingListsIndexSchema.Cut(list.Name, MailingListsIndexSchema.NameLength),
                ProviderName = MailingListsIndexSchema.Cut(list.ProviderName, MailingListsIndexSchema.ProviderNameLength),
                IsEnabled = list.IsEnabled,
            });
}
