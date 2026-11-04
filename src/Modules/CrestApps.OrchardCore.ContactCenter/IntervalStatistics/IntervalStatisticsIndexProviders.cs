using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.IntervalStatistics;

/// <summary>
/// Maps <see cref="IntervalStatisticsDay"/> documents to the <see cref="IntervalStatisticsDayIndex"/>.
/// </summary>
public sealed class IntervalStatisticsDayIndexProvider : IndexProvider<IntervalStatisticsDay>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsDayIndexProvider"/> class.
    /// </summary>
    public IntervalStatisticsDayIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<IntervalStatisticsDay> context)
    {
        context
            .For<IntervalStatisticsDayIndex>()
            .Map(day => new IntervalStatisticsDayIndex
            {
                ItemId = day.ItemId,
                Date = day.Date,
                Version = day.Version,
                Final = day.Final,
                InteractionCount = day.InteractionCount,
                ComputedUtc = day.ComputedUtc,
            });
    }
}

/// <summary>
/// Maps <see cref="QueueIntervalStatistics"/> documents to the <see cref="QueueIntervalStatisticsIndex"/>.
/// </summary>
public sealed class QueueIntervalStatisticsIndexProvider : IndexProvider<QueueIntervalStatistics>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueIntervalStatisticsIndexProvider"/> class.
    /// </summary>
    public QueueIntervalStatisticsIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<QueueIntervalStatistics> context)
    {
        context
            .For<QueueIntervalStatisticsIndex>()
            .Map(day => new QueueIntervalStatisticsIndex
            {
                ItemId = day.ItemId,
                QueueId = day.QueueId,
                Date = day.Date,
            });
    }
}

/// <summary>
/// Maps <see cref="AgentIntervalStatistics"/> documents to the <see cref="AgentIntervalStatisticsIndex"/>.
/// </summary>
public sealed class AgentIntervalStatisticsIndexProvider : IndexProvider<AgentIntervalStatistics>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentIntervalStatisticsIndexProvider"/> class.
    /// </summary>
    public AgentIntervalStatisticsIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentIntervalStatistics> context)
    {
        context
            .For<AgentIntervalStatisticsIndex>()
            .Map(day => new AgentIntervalStatisticsIndex
            {
                ItemId = day.ItemId,
                AgentId = day.AgentId,
                UserId = day.UserId,
                Date = day.Date,
            });
    }
}
