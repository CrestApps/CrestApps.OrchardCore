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
    /// Gets or sets the transfer destination type, such as agent, queue, external, or entry point.
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
    /// Gets or sets the transfer result.
    /// </summary>
    public string Result { get; set; }
}
