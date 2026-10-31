using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A worker's offer of one of their published shifts.
/// </summary>
public sealed class ShiftTradeOffer
{
    /// <summary>
    /// Gets or sets what the trade does.
    /// </summary>
    public ShiftTradeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the calendar event of the shift offered; its worker is the one offering.
    /// </summary>
    public string ShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets the colleague it is offered to; required for a swap, optional for a give-away.
    /// </summary>
    public string TargetWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the colleague's shift wanted in exchange, for a swap.
    /// </summary>
    public string TargetShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets a note to the colleague.
    /// </summary>
    public string Note { get; set; }
}
