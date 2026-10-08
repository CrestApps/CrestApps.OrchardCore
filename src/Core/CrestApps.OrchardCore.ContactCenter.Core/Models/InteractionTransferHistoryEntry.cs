namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a transfer attempt recorded as part of an interaction's communication history.
/// </summary>
public sealed class InteractionTransferHistoryEntry
{
    /// <summary>
    /// Gets or sets the participant or agent that initiated the transfer.
    /// </summary>
    public string FromParticipantId { get; set; }

    /// <summary>
    /// Gets or sets the transfer destination identifier.
    /// </summary>
    public string ToParticipantId { get; set; }

    /// <summary>
    /// Gets or sets the transfer destination type recorded as a historical text snapshot of the
    /// <see cref="CrestApps.OrchardCore.ContactCenter.Models.InteractionTransferTargetType"/> name at the time of the
    /// transfer. This is an audit value for display only; the live topology exposes the typed target and this string is
    /// never re-parsed back into the enum.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the transfer was requested.
    /// </summary>
    public DateTime RequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the transfer completed or failed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the human-readable transfer result recorded as a historical text snapshot for audit display. It is
    /// descriptive text, not a machine outcome, and is never re-parsed into a typed result.
    /// </summary>
    public string Result { get; set; }
}
