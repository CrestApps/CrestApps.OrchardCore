using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to find the pacing record of a campaign queue.
/// </summary>
public sealed class PredictivePacingStateIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the campaign queue the record paces.
    /// </summary>
    public string QueueId { get; set; }
}
