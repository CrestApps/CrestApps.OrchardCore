using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps TimeOffAllotment records to <see cref="TimeOffAllotmentIndex"/> rows.
/// </summary>
public sealed class TimeOffAllotmentIndexProvider : IndexProvider<TimeOffAllotment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAllotmentIndexProvider"/> class.
    /// </summary>
    public TimeOffAllotmentIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TimeOffAllotment> context)
    {
        context.For<TimeOffAllotmentIndex>()
            .Map(item => new TimeOffAllotmentIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                TeamId = item.TeamId,
                Enabled = item.Enabled,
            });
    }
}
