using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterEventMetricDelta"/> documents to the <see cref="ContactCenterEventMetricDeltaIndex"/>.
/// </summary>
public sealed class ContactCenterEventMetricDeltaIndexProvider : IndexProvider<ContactCenterEventMetricDelta>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricDeltaIndexProvider"/> class.
    /// </summary>
    public ContactCenterEventMetricDeltaIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterEventMetricDelta> context)
    {
        context
            .For<ContactCenterEventMetricDeltaIndex>()
            .Map(delta => new ContactCenterEventMetricDeltaIndex
            {
                ItemId = delta.ItemId,
                DateKey = delta.DateKey,
                Date = delta.Date,
                EventType = delta.EventType,
                Count = delta.Count,
                CreatedUtc = delta.CreatedUtc,
            });
    }
}
