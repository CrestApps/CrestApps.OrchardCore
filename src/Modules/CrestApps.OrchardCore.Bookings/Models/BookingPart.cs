using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Bookings.Models;

/// <summary>
/// Makes a product a booking: what is booked (a departure, a stay or a time), how many can be on each date, how it's
/// priced for the people in it, and what paying and canceling it takes.
/// </summary>
public sealed class BookingPart : ContentPart
{
    public BookingKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how many can be booked on a date: seats on a departure, rooms a night, or bookings in a time slot.
    /// A departure's own dates can say otherwise.
    /// </summary>
    public int Capacity { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the product's price is per person, such as a tour's, rather than per unit, such as a
    /// room's. A departure's seats are always counted per person.
    /// </summary>
    public bool PricePerPerson { get; set; }

    /// <summary>
    /// Gets or sets what a unit can hold, such as a single or a double room, each with how many people it's for and
    /// what it adds to or takes off the price, such as a single supplement. None means one person a unit.
    /// </summary>
    public List<BookingOccupancy> Occupancies { get; set; } = [];

    /// <summary>
    /// Gets or sets the fewest nights a stay can be.
    /// </summary>
    public int MinNights { get; set; } = 1;

    /// <summary>
    /// Gets or sets the most nights a stay can be.
    /// </summary>
    public int MaxNights { get; set; } = 28;

    /// <summary>
    /// Gets or sets the price of a Friday or Saturday night, or <see langword="null"/> for the product's price.
    /// </summary>
    public decimal? WeekendPrice { get; set; }

    /// <summary>
    /// Gets or sets when time slots are offered, by day of the week.
    /// </summary>
    public List<BookingOpeningHours> Hours { get; set; } = [];

    /// <summary>
    /// Gets or sets how long a time slot is, in minutes.
    /// </summary>
    public int SlotMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets how many days ahead stays and time slots can be booked.
    /// </summary>
    public int DaysAhead { get; set; } = 180;

    /// <summary>
    /// Gets or sets the percent of the price a buyer can pay now as a deposit, or zero to pay in full.
    /// </summary>
    public decimal DepositPercent { get; set; }

    /// <summary>
    /// Gets or sets how many days before the date the rest is due and charged. A booking closer than that is paid in
    /// full.
    /// </summary>
    public int BalanceDueDaysBefore { get; set; } = 30;

    /// <summary>
    /// Gets or sets what a cancellation costs by how long before the date it is, such as 10% up to 60 days before and
    /// 50% up to 30. None means a booking can be canceled for a full refund.
    /// </summary>
    public List<BookingCancellationTier> CancellationTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the store confirms each booking before the buyer pays: the buyer sends a request, and gets
    /// a pay link when it's confirmed.
    /// </summary>
    public bool RequestToBook { get; set; }

    /// <summary>
    /// Gets or sets whether the buyer gives each traveler's, or attendee's, details at checkout, from the Traveler
    /// content type's fields.
    /// </summary>
    public bool AskForTravelers { get; set; }
}

/// <summary>
/// What is booked.
/// </summary>
public enum BookingKind
{
    /// <summary>
    /// A fixed date with seats, such as a tour, an event or a class.
    /// </summary>
    Departure = 0,

    /// <summary>
    /// Nights from a check-in to a check-out, such as a room or a rental.
    /// </summary>
    Nightly = 1,

    /// <summary>
    /// A time on a day, such as an appointment.
    /// </summary>
    TimeSlot = 2,
}

/// <summary>
/// What a unit holds, such as a double room for two.
/// </summary>
public sealed class BookingOccupancy
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the buyer sees, such as "Double room".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets how many people one unit is for.
    /// </summary>
    public int People { get; set; } = 1;

    /// <summary>
    /// Gets or sets what one unit adds to the price, such as a single supplement, or takes off it, such as a child's
    /// price, in the product's currency, once per booking of it (not per night).
    /// </summary>
    public decimal PriceAdjustment { get; set; }
}

/// <summary>
/// When time slots are offered on a day of the week.
/// </summary>
public sealed class BookingOpeningHours
{
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets when the first slot starts, as <c>HH:mm</c>.
    /// </summary>
    public string Opens { get; set; }

    /// <summary>
    /// Gets or sets when the last slot ends, as <c>HH:mm</c>.
    /// </summary>
    public string Closes { get; set; }
}

/// <summary>
/// What canceling costs from some days before the date.
/// </summary>
public sealed class BookingCancellationTier
{
    /// <summary>
    /// Gets or sets the fewest days before the date a cancellation is, for this fee.
    /// </summary>
    public int DaysBefore { get; set; }

    /// <summary>
    /// Gets or sets the percent of the booking's price the store keeps.
    /// </summary>
    public decimal FeePercent { get; set; }
}
