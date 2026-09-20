using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
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
        CollectionName = ContactCenterStorage.CollectionName;
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
                ProcessedUtc = processedEvent.ProcessedUtc,
            });
    }
}
