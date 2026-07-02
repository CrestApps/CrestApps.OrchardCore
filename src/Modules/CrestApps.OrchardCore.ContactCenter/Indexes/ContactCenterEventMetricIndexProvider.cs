using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterEventMetric"/> documents to the <see cref="ContactCenterEventMetricIndex"/>.
/// </summary>
public sealed class ContactCenterEventMetricIndexProvider : IndexProvider<ContactCenterEventMetric>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricIndexProvider"/> class.
    /// </summary>
    public ContactCenterEventMetricIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterEventMetric> context)
    {
        context
            .For<ContactCenterEventMetricIndex>()
            .Map(metric => new ContactCenterEventMetricIndex
            {
                ItemId = metric.ItemId,
                DateKey = metric.DateKey,
                Date = metric.Date,
                EventType = metric.EventType,
            });
    }
}
