using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterProcessedEvent"/> documents to the <see cref="ContactCenterProcessedEventIndex"/>.
/// </summary>
public sealed class ContactCenterProcessedEventIndexProvider : IndexProvider<ContactCenterProcessedEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventIndexProvider"/> class.
    /// </summary>
    public ContactCenterProcessedEventIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterProcessedEvent> context)
    {
        context
            .For<ContactCenterProcessedEventIndex>()
            .Map(processedEvent => new ContactCenterProcessedEventIndex
            {
                ItemId = processedEvent.ItemId,
                HandlerId = processedEvent.HandlerId,
                EventId = processedEvent.EventId,
            });
    }
}
