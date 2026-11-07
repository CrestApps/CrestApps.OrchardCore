using CrestApps.OrchardCore.Outreach.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Outreach.Core.Indexes;

/// <summary>
/// Maps outreach batches to the <see cref="OutreachBatchIndex"/>.
/// </summary>
public sealed class OutreachBatchIndexProvider : IndexProvider<OutreachBatch>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachBatchIndexProvider"/> class.
    /// </summary>
    public OutreachBatchIndexProvider()
    {
        CollectionName = OutreachConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<OutreachBatch> context)
    {
        context
            .For<OutreachBatchIndex>()
            .Map(batch => new OutreachBatchIndex
            {
                ItemId = batch.ItemId,
                RunId = batch.RunId,
                Status = batch.Status,
                NextAttemptUtc = batch.NextAttemptUtc,
            });
    }
}
