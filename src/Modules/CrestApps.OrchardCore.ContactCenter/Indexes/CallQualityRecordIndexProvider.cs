using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="CallQualityRecord"/> documents to the <see cref="CallQualityRecordIndex"/>.
/// </summary>
public sealed class CallQualityRecordIndexProvider : IndexProvider<CallQualityRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallQualityRecordIndexProvider"/> class.
    /// </summary>
    public CallQualityRecordIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallQualityRecord> context)
    {
        context
            .For<CallQualityRecordIndex>()
            .Map(record => new CallQualityRecordIndex
            {
                ItemId = record.ItemId,
                RecordKey = record.RecordKey,
                Source = record.Source,
                Rating = record.Rating,
                InteractionId = record.InteractionId,
                AgentId = record.AgentId,
                QueueId = record.QueueId,
                ObservedUtc = record.ObservedUtc,
            });
    }
}
