using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OmnichannelActivityBatchIndexProvider : IndexProvider<OmnichannelActivityBatch>
{
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
                Channel = batch.Channel,
                BatchId = batch.Id,
                DisplayText = !string.IsNullOrEmpty(batch.DisplayText)
                    ? batch.DisplayText.Substring(0, Math.Min(255, batch.DisplayText.Length))
                    : null,
                Status = batch.Status,
            });
    }
}
