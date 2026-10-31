using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps shift trades to <see cref="ShiftTradeIndex"/> rows.
/// </summary>
public sealed class ShiftTradeIndexProvider : IndexProvider<ShiftTrade>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTradeIndexProvider"/> class.
    /// </summary>
    public ShiftTradeIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ShiftTrade> context)
    {
        context.For<ShiftTradeIndex>()
            .Map(item => new ShiftTradeIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                Status = item.Status,
                OfferedByWorkerId = item.OfferedByWorkerId,
                TargetWorkerId = item.TargetWorkerId,
                ShiftEventId = item.ShiftEventId,
                TargetShiftEventId = item.TargetShiftEventId,
                ShiftStartUtc = item.ShiftStartUtc,
                CreatedUtc = item.CreatedUtc,
            });
    }
}
