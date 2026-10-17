using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="AgentDailyTimeSummary"/> documents to the <see cref="AgentDailyTimeSummaryIndex"/>.
/// </summary>
public sealed class AgentDailyTimeSummaryIndexProvider : IndexProvider<AgentDailyTimeSummary>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentDailyTimeSummaryIndexProvider"/> class.
    /// </summary>
    public AgentDailyTimeSummaryIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentDailyTimeSummary> context)
    {
        context
            .For<AgentDailyTimeSummaryIndex>()
            .Map(summary => new AgentDailyTimeSummaryIndex
            {
                ItemId = summary.ItemId,
                Date = summary.Date,
                Version = summary.Version,
                EventCount = summary.EventCount,
            });
    }
}
