using System.Text.Json.Nodes;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Bookings.Models;

/// <summary>
/// One date of a booking product and how much of it is taken: a departure, a night or a time slot. A departure's dates
/// are added by the store; a night or a time slot is made the first time it's held, or when the store changes it.
/// </summary>
public sealed class BookingSlot : CatalogItem
{
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the date, as <c>yyyy-MM-dd</c>, or the time, as <c>yyyy-MM-ddTHH:mm</c>, in the site's time zone.
    /// </summary>
    public string SlotKey { get; set; }

    public DateTime StartsUtc { get; set; }

    public int Capacity { get; set; }

    /// <summary>
    /// Gets or sets how much unpaid orders hold.
    /// </summary>
    public int Held { get; set; }

    /// <summary>
    /// Gets or sets how much paid bookings take.
    /// </summary>
    public int Sold { get; set; }

    /// <summary>
    /// Gets or sets the date's own price, in the product's currency, in place of the product's, such as a departure
    /// in high season, or <see langword="null"/>.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets whether the date can't be booked any more.
    /// </summary>
    public bool Closed { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public int Remaining
        => Math.Max(0, Capacity - Held - Sold);
}

/// <summary>
/// A booked order line: what was booked, for when, how much of the dates it takes, and where it is.
/// </summary>
public sealed class Booking : CatalogItem
{
    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets the cart the order was placed from, so that cart's own unpaid order doesn't count against it.
    /// </summary>
    public string CartId { get; set; }

    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public BookingKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the dates it takes: the departure, every night of the stay, or the time slot.
    /// </summary>
    public List<string> SlotKeys { get; set; } = [];

    public DateTime StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets how many units were booked, such as rooms.
    /// </summary>
    public int Units { get; set; }

    /// <summary>
    /// Gets or sets how many people in all.
    /// </summary>
    public int People { get; set; }

    /// <summary>
    /// Gets or sets how much of each date it takes: seats for a departure, units otherwise.
    /// </summary>
    public int Seats { get; set; }

    public string OccupancyLabel { get; set; }

    public BookingStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when an unpaid booking's hold on its dates ends, or <see langword="null"/> for a request the store
    /// hasn't answered, which holds them until it's declined or expires.
    /// </summary>
    public DateTime? HoldExpiresUtc { get; set; }

    public string UserId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets each traveler's details, as the Traveler content type's parts and fields.
    /// </summary>
    public List<JsonObject> Travelers { get; set; } = [];

    /// <summary>
    /// Gets or sets the cancellation policy as it was when it was booked.
    /// </summary>
    public List<BookingCancellationTier> CancellationTiers { get; set; } = [];

    public decimal? CancellationFee { get; set; }

    public decimal? Refunded { get; set; }

    public string CancelReason { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? ConfirmedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public DateTime? CanceledUtc { get; set; }
}

public enum BookingStatus
{
    /// <summary>
    /// The order isn't paid yet; its dates are held for it.
    /// </summary>
    Held = 0,

    /// <summary>
    /// The buyer asked, and the store hasn't confirmed it yet; its dates are held for it.
    /// </summary>
    Requested = 1,

    /// <summary>
    /// Paid, or a deposit paid, and booked.
    /// </summary>
    Confirmed = 2,

    Completed = 3,

    /// <summary>
    /// The buyer didn't come.
    /// </summary>
    NoShow = 4,

    Canceled = 5,

    /// <summary>
    /// The order wasn't paid in time, and its dates were given back.
    /// </summary>
    Expired = 6,
}

/// <summary>
/// What's left to pay on an order whose bookings were paid by deposit: owed on the ledger, due some days before the
/// earliest of them, and charged then to the card the deposit was paid with.
/// </summary>
public sealed class BookingBalance : CatalogItem
{
    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the ledger transaction the balance is owed on.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the checkout that paid the deposit and kept the card.
    /// </summary>
    public string DepositSessionId { get; set; }

    public string OwnerId { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public DateTime DueUtc { get; set; }

    public BookingBalanceStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTime? LastAttemptUtc { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public string LastFailure { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public enum BookingBalanceStatus
{
    /// <summary>
    /// Charged to the kept card when it's due.
    /// </summary>
    Scheduled = 0,

    Paid = 1,

    /// <summary>
    /// The card was declined every time; the buyer pays it from the reminder's pay link.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Not owed any more, such as after a cancellation.
    /// </summary>
    Canceled = 3,
}
