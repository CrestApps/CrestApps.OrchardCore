using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of shift trades.
/// </summary>
public sealed class ShiftTradeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public ShiftTradeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the worker who offered it.
    /// </summary>
    public string OfferedByWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the colleague it is offered to.
    /// </summary>
    public string TargetWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the offered shift's event.
    /// </summary>
    public string ShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets the colleague's shift's event, for a swap.
    /// </summary>
    public string TargetShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets when the offered shift starts.
    /// </summary>
    public DateTime ShiftStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was offered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
