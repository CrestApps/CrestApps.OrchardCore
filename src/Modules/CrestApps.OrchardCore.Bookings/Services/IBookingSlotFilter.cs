using CrestApps.OrchardCore.Bookings.Models;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Closes some of a product's dates or times for a reason Bookings doesn't track itself, such as the Bookings Calendar
/// feature closing the times that are busy on a calendar. A closed date or time shows as full and can't be held.
/// </summary>
public interface IBookingSlotFilter
{
    /// <summary>
    /// Returns which of the date or time keys are closed.
    /// </summary>
    /// <param name="contentItemId">The product.</param>
    /// <param name="part">The product's booking settings.</param>
    /// <param name="slotKeys">The date or time keys, such as <c>2026-11-10</c> or <c>2026-11-10T09:00</c>.</param>
    /// <param name="timeZone">The time zone the keys are in.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The closed keys.</returns>
    Task<IReadOnlyCollection<string>> GetClosedSlotKeysAsync(string contentItemId, BookingPart part, IReadOnlyList<string> slotKeys, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);
}
