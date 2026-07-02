using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ActivityQueue"/> documents to the <see cref="ActivityQueueIndex"/>.
/// </summary>
public sealed class ActivityQueueIndexProvider : IndexProvider<ActivityQueue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueIndexProvider"/> class.
    /// </summary>
    public ActivityQueueIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ActivityQueue> context)
    {
        context
            .For<ActivityQueueIndex>()
            .Map(queue => new ActivityQueueIndex
            {
                ItemId = queue.ItemId,
                Name = queue.Name,
                Enabled = queue.Enabled,
            });
    }
}
