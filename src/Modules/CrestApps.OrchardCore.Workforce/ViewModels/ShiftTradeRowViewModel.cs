using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of the shift trade list.
/// </summary>
public class ShiftTradeRowViewModel
{
    /// <summary>
    /// Gets or sets the trade.
    /// </summary>
    public ShiftTrade Trade { get; set; }

    /// <summary>
    /// Gets or sets the name of its team.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the name of the worker who offered it.
    /// </summary>
    public string OfferedByName { get; set; }

    /// <summary>
    /// Gets or sets the name of the colleague it is offered to.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the name of the worker who accepted it.
    /// </summary>
    public string AcceptedByName { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }
}
