using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's offer to give away or swap a published shift, and what came of it.
/// </summary>
public sealed class ShiftTrade : CatalogItem
{
    /// <summary>
    /// Gets or sets the team of the offered shift.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets what the trade does.
    /// </summary>
    public ShiftTradeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public ShiftTradeStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the worker who offered it.
    /// </summary>
    public string OfferedByWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the calendar event of the offered shift.
    /// </summary>
    public string ShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets the offered shift's key.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets when the offered shift starts.
    /// </summary>
    public DateTime ShiftStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the offered shift ends.
    /// </summary>
    public DateTime ShiftEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the colleague it is offered to; <see langword="null"/> for a give-away anyone eligible can pick up.
    /// </summary>
    public string TargetWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the calendar event of the colleague's shift, for a swap.
    /// </summary>
    public string TargetShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets when the colleague's shift starts, for a swap.
    /// </summary>
    public DateTime? TargetShiftStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the colleague's shift ends, for a swap.
    /// </summary>
    public DateTime? TargetShiftEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the worker who accepted it.
    /// </summary>
    public string AcceptedByWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it was accepted.
    /// </summary>
    public DateTime? AcceptedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether a manager must approve it once accepted.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Gets or sets who approved or declined it.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was approved, declined, rejected or canceled.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the offering worker's note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the note of whoever decided.
    /// </summary>
    public string DecisionNote { get; set; }

    /// <summary>
    /// Gets or sets who created the offer, the worker or a manager on their behalf.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was offered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
