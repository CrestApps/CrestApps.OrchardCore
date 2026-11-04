using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps draft shifts to <see cref="DraftShiftIndex"/> rows.
/// </summary>
public sealed class DraftShiftIndexProvider : IndexProvider<DraftShift>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DraftShiftIndexProvider"/> class.
    /// </summary>
    public DraftShiftIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<DraftShift> context)
    {
        context.For<DraftShiftIndex>()
            .Map(item => new DraftShiftIndex
            {
                ItemId = item.ItemId,
                ScenarioId = item.ScenarioId,
                PeriodId = item.PeriodId,
                WorkerId = item.WorkerId,
                Date = WorkforceDates.ToKey(item.Date),
            });
    }
}
