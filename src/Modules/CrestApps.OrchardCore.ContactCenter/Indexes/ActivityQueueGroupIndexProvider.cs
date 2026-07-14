using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ActivityQueueGroup"/> documents to the <see cref="ActivityQueueGroupIndex"/>.
/// </summary>
public sealed class ActivityQueueGroupIndexProvider : IndexProvider<ActivityQueueGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupIndexProvider"/> class.
    /// </summary>
    public ActivityQueueGroupIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ActivityQueueGroup> context)
    {
        context
            .For<ActivityQueueGroupIndex>()
            .Map(group => new ActivityQueueGroupIndex
            {
                ItemId = group.ItemId,
                Name = group.Name,
            });
    }
}
