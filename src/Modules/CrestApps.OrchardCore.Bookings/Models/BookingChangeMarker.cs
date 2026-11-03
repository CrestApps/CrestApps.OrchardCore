namespace CrestApps.OrchardCore.Bookings.Models;

/// <summary>
/// What a booking was when it was last saved, kept on the booking so the next save can tell what changed.
/// </summary>
public sealed class BookingChangeMarker
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the booking started.
    /// </summary>
    public DateTime StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the booking ended.
    /// </summary>
    public DateTime? EndsUtc { get; set; }
}
