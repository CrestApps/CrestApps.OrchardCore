using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelActivityBatchIndex : MapIndex
{
    public string BatchId { get; set; }

    public string Channel { get; set; }

    public string DisplayText { get; set; }

    public OmnichannelActivityBatchStatus Status { get; set; }
}
