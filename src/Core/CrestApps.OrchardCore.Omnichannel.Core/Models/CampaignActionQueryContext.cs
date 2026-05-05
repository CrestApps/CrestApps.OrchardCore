using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class CampaignActionQueryContext : QueryContext
{
    /// <summary>
    /// Gets or sets the campaign identifier to filter actions by.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier to filter actions by.
    /// </summary>
    public string DispositionId { get; set; }
}
