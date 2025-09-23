using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelActivityBatchIndex : CatalogItemIndex
{
    public string Channel { get; set; }

    public string DisplayText { get; set; }

    public OmnichannelActivityBatchStatus Status { get; set; }
}
