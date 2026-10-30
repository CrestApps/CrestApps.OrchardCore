using CrestApps.OrchardCore.Bookings.Models;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// A booking being saved, with what it was when it was last saved.
/// </summary>
public sealed class BookingSavingContext
{
    /// <summary>
    /// Gets or sets the booking.
    /// </summary>
    public Booking Booking { get; set; }

    /// <summary>
    /// Gets or sets whether the booking is new.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the status the booking had when it was last saved, or <see langword="null"/> when it is new or was saved
    /// before its changes were followed.
    /// </summary>
    public BookingStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets when the booking started when it was last saved, or <see langword="null"/> when it is not known.
    /// </summary>
    public DateTime? PreviousStartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the booking ended when it was last saved, or <see langword="null"/> when it is not known.
    /// </summary>
    public DateTime? PreviousEndsUtc { get; set; }

    /// <summary>
    /// Gets whether the status changed since the booking was last saved.
    /// </summary>
    public bool StatusChanged => PreviousStatus.HasValue && PreviousStatus.Value != Booking.Status;

    /// <summary>
    /// Gets whether the booking moved to other dates since it was last saved.
    /// </summary>
    public bool Rescheduled => PreviousStartsUtc.HasValue && (PreviousStartsUtc.Value != Booking.StartsUtc || PreviousEndsUtc != Booking.EndsUtc);
}
