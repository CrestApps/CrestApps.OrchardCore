using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Maps <see cref="SchedulingResource"/> documents to <see cref="SchedulingResourceIndex"/> rows.
/// </summary>
public sealed class SchedulingResourceIndexProvider : IndexProvider<SchedulingResource>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourceIndexProvider"/> class.
    /// </summary>
    public SchedulingResourceIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SchedulingResource> context)
    {
        context.For<SchedulingResourceIndex>()
            .Map(resource => new SchedulingResourceIndex
            {
                ItemId = resource.ItemId,
                Name = resource.Name,
                UserId = resource.UserId,
                Enabled = resource.Enabled,
            });
    }
}
