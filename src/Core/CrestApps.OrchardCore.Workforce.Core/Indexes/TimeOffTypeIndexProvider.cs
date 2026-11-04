using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffType records to <see cref="TimeOffTypeIndex"/> rows.
/// </summary>
public sealed class TimeOffTypeIndexProvider : IndexProvider<TimeOffType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffTypeIndexProvider"/> class.
    /// </summary>
    public TimeOffTypeIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffType> context)
    {
        context.For<TimeOffTypeIndex>()
            .Map(item => new TimeOffTypeIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}
