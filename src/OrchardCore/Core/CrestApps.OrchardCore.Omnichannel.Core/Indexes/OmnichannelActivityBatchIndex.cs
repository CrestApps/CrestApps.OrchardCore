using CrestApps.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

public sealed class OmnichannelActivityBatchIndex : CatalogItemIndex
{
    public string DisplayText { get; set; }

    public OmnichannelActivityBatchStatus Status { get; set; }
}
