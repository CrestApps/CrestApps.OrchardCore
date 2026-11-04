using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps schedule periods to <see cref="SchedulePeriodIndex"/> rows.
/// </summary>
public sealed class SchedulePeriodIndexProvider : IndexProvider<SchedulePeriod>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulePeriodIndexProvider"/> class.
    /// </summary>
    public SchedulePeriodIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SchedulePeriod> context)
    {
        context.For<SchedulePeriodIndex>()
            .Map(item => new SchedulePeriodIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                StartDate = WorkforceDates.ToKey(item.StartDate),
                EndDate = WorkforceDates.ToKey(item.EndDate),
                Status = item.Status,
            });
    }
}
