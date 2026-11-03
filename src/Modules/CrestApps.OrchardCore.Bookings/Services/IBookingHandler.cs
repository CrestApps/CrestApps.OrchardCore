using CrestApps.OrchardCore.Bookings.Models;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Hears about bookings as they are saved and deleted, so other features can follow them: the Bookings Calendar feature keeps a
/// calendar event for each booking, and the workflow integration raises the booking workflow events once the change is
/// committed. A handler runs inside the save, so what it changes is committed with the booking.
/// </summary>
public interface IBookingHandler
{
    /// <summary>
    /// Runs when a booking is about to be saved.
    /// </summary>
    /// <param name="context">The booking and what changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SavingAsync(BookingSavingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when a booking is about to be deleted.
    /// </summary>
    /// <param name="booking">The booking.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DeletingAsync(Booking booking, CancellationToken cancellationToken = default);
}
