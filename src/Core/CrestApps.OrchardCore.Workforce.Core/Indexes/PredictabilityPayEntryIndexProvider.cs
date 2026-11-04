using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps PredictabilityPayEntry records to <see cref="PredictabilityPayEntryIndex"/> rows.
/// </summary>
public sealed class PredictabilityPayEntryIndexProvider : IndexProvider<PredictabilityPayEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PredictabilityPayEntryIndexProvider"/> class.
    /// </summary>
    public PredictabilityPayEntryIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PredictabilityPayEntry> context)
    {
        context.For<PredictabilityPayEntryIndex>()
            .Map(item => new PredictabilityPayEntryIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                WorkerId = item.WorkerId,
                ScheduleChangeId = item.ScheduleChangeId,
                Status = item.Status,
                CreatedUtc = item.CreatedUtc,
            });
    }
}
