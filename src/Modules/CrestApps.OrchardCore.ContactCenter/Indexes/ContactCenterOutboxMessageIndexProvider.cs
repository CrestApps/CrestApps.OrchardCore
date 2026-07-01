using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterOutboxMessage"/> documents to the <see cref="ContactCenterOutboxMessageIndex"/>.
/// </summary>
public sealed class ContactCenterOutboxMessageIndexProvider : IndexProvider<ContactCenterOutboxMessage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxMessageIndexProvider"/> class.
    /// </summary>
    public ContactCenterOutboxMessageIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterOutboxMessage> context)
    {
        context
            .For<ContactCenterOutboxMessageIndex>()
            .Map(message => new ContactCenterOutboxMessageIndex
            {
                ItemId = message.ItemId,
                EventId = message.EventId,
                Status = message.Status,
                NextAttemptUtc = message.NextAttemptUtc,
            });
    }
}
