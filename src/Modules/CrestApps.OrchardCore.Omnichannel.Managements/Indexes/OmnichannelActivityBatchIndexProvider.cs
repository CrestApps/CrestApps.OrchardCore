using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OmnichannelActivityBatchIndexProvider : IndexProvider<OmnichannelActivityBatch>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchIndexProvider"/> class.
    /// </summary>
    public OmnichannelActivityBatchIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
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
                Status = batch.Status,
            });
    }
}
