namespace CrestApps.OrchardCore.Bookings;

public static class BookingsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Bookings";

    /// <summary>
    /// The feature that keeps every booking on a Bookings calendar.
    /// </summary>
    public const string CalendarFeature = "CrestApps.OrchardCore.Bookings.Calendar";

    /// <summary>
    /// The YesSql collection that holds dates, bookings and balances.
    /// </summary>
    public const string CollectionName = "Bookings";

    public const string PartName = "BookingPart";

    /// <summary>
    /// The content type whose fields are asked for each traveler, or attendee, of a booking.
    /// </summary>
    public const string TravelerContentType = "Traveler";

    /// <summary>
    /// The tender a booking's balance is put on: owed later, and charged to the card the deposit was paid with.
    /// </summary>
    public const string BalanceTenderKey = "booking-balance";

    public const string TransactionSource = "Bookings";

    /// <summary>
    /// The keys a booking keeps on its cart and order line.
    /// </summary>
    public static class Details
    {
        /// <summary>
        /// The date of a departure or a stay's first night (<c>yyyy-MM-dd</c>), or a time slot's start (<c>yyyy-MM-ddTHH:mm</c>).
        /// </summary>
        public const string Date = "booking.date";

        /// <summary>
        /// A stay's check-out date (<c>yyyy-MM-dd</c>).
        /// </summary>
        public const string End = "booking.end";

        /// <summary>
        /// The occupancy chosen, such as a double room.
        /// </summary>
        public const string Occupancy = "booking.occupancy";
    }
}
