using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// The list of phone numbers known not to be in service.
/// </summary>
public class NotInServiceNumbersIndexViewModel
{
    /// <summary>
    /// Gets or sets the digits the listed numbers must contain.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the numbers on this page.
    /// </summary>
    public IList<NotInServiceNumberEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of marked numbers matching the search.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}

/// <summary>
/// One number in the list of numbers known not to be in service.
/// </summary>
public class NotInServiceNumberEntry
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
