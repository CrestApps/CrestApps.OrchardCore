using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps schedule scenarios to <see cref="ScheduleScenarioIndex"/> rows.
/// </summary>
public sealed class ScheduleScenarioIndexProvider : IndexProvider<ScheduleScenario>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleScenarioIndexProvider"/> class.
    /// </summary>
    public ScheduleScenarioIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ScheduleScenario> context)
    {
        context.For<ScheduleScenarioIndex>()
            .Map(item => new ScheduleScenarioIndex
            {
                ItemId = item.ItemId,
                PeriodId = item.PeriodId,
            });
    }
}
