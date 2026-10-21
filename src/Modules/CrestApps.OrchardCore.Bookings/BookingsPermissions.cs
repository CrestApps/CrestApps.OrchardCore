using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Bookings;

public static class BookingsPermissions
{
    /// <summary>
    /// Sees and changes bookings and their dates: confirms, completes and cancels them, and charges balances.
    /// </summary>
    public static readonly Permission ManageBookings = new(
        "ManageBookings",
        LocalizationSource.Create("Manage bookings", typeof(BookingsPermissions)));
}
