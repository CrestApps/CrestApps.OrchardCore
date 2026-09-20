using CrestApps.Core.Omnichannel;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Omnichannel.Indexes;

public sealed class OmnichannelActivityBatchIndexProvider : IndexProvider<OmnichannelActivityBatch>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchIndexProvider"/> class.
    /// </summary>
    public OmnichannelActivityBatchIndexProvider()
    {
        CollectionName = OmnichannelCollections.Name;
    }

    public override void Describe(DescribeContext<OmnichannelActivityBatch> context)
    {
        context
            .For<OmnichannelActivityBatchIndex>()
            .Map(batch => new OmnichannelActivityBatchIndex
            {
                ItemId = batch.ItemId,
                DisplayText = !string.IsNullOrEmpty(batch.DisplayText)
            ? batch.DisplayText.Substring(0, Math.Min(255, batch.DisplayText.Length))
            : null,
                Source = batch.Source,
                Status = batch.Status,
                CreatedUtc = batch.CreatedUtc,
            });
    }
}
