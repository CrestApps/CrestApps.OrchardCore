using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Omnichannel.Indexes;

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
    /// Gets or sets the activity source used when loading activities from this batch.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public OmnichannelActivityBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity batch was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
