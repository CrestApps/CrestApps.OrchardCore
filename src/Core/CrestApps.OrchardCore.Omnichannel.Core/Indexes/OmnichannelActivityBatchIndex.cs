using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the omnichannel activity batch index.
/// </summary>
public sealed class OmnichannelActivityBatchIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OmnichannelActivityBatchStatus Status { get; set; }
}
