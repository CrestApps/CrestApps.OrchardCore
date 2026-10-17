using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// One number in the list of numbers known not to be in service.
/// </summary>
public sealed class NotInServiceNumberEntry
{
    /// <summary>
    /// Gets or sets the mark.
    /// </summary>
    public NotInServiceNumber Number { get; set; }

    /// <summary>
    /// Gets or sets the name of the campaign whose attempt found the number out of service, when there was one.
    /// </summary>
    public string CampaignName { get; set; }
}
