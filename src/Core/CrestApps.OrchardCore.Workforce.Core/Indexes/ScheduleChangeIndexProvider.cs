using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps published schedule changes to <see cref="ScheduleChangeIndex"/> rows.
/// </summary>
public sealed class ScheduleChangeIndexProvider : IndexProvider<ScheduleChange>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleChangeIndexProvider"/> class.
    /// </summary>
    public ScheduleChangeIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ScheduleChange> context)
    {
        context.For<ScheduleChangeIndex>()
            .Map(item => new ScheduleChangeIndex
            {
                ItemId = item.ItemId,
                PeriodId = item.PeriodId,
                TeamId = item.TeamId,
                WorkerId = item.WorkerId,
                ChangedUtc = item.ChangedUtc,
            });
    }
}
