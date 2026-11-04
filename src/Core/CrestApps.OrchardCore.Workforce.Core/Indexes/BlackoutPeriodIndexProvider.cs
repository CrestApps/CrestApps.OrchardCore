using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps BlackoutPeriod records to <see cref="BlackoutPeriodIndex"/> rows.
/// </summary>
public sealed class BlackoutPeriodIndexProvider : IndexProvider<BlackoutPeriod>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlackoutPeriodIndexProvider"/> class.
    /// </summary>
    public BlackoutPeriodIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<BlackoutPeriod> context)
    {
        context.For<BlackoutPeriodIndex>()
            .Map(item => new BlackoutPeriodIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                TeamId = item.TeamId,
                StartDate = WorkforceDates.ToKey(item.StartDate),
                EndDate = WorkforceDates.ToKey(item.EndDate),
                Enabled = item.Enabled,
            });
    }
}
