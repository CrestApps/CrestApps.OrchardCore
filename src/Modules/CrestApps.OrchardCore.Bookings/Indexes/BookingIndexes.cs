using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Bookings.Indexes;

public sealed class BookingSlotIndex : CatalogItemIndex
{
    public string ContentItemId { get; set; }

    public string SlotKey { get; set; }

    public DateTime StartsUtc { get; set; }

    public int Remaining { get; set; }

    public bool Closed { get; set; }
}

public sealed class BookingIndex : CatalogItemIndex
{
    public string OrderId { get; set; }

    public string ContentItemId { get; set; }

    public string CartId { get; set; }

    public string Title { get; set; }

    public string CustomerName { get; set; }

    public DateTime StartsUtc { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime? HoldExpiresUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user the booking was made by, so a privacy request finds it.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the booker's email, trimmed and lowercase, so a privacy request finds a guest's bookings.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Returns an email as it is indexed: trimmed, lowercase and at most 255 characters.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns>The indexed email, or <see langword="null"/>.</returns>
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();

        return normalized.Length > 255 ? normalized[..255] : normalized;
    }
}

public sealed class BookingBalanceIndex : CatalogItemIndex
{
    public string OrderId { get; set; }

    public BookingBalanceStatus Status { get; set; }

    public DateTime DueUtc { get; set; }

    public DateTime? NextAttemptUtc { get; set; }
}

public sealed class BookingSlotIndexProvider : IndexProvider<BookingSlot>
{
    public BookingSlotIndexProvider()
    {
        CollectionName = BookingsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<BookingSlot> context)
        => context.For<BookingSlotIndex>()
            .Map(slot => new BookingSlotIndex
            {
                ItemId = slot.ItemId,
                ContentItemId = slot.ContentItemId,
                SlotKey = slot.SlotKey,
                StartsUtc = slot.StartsUtc,
                Remaining = slot.Remaining,
                Closed = slot.Closed,
            });
}

public sealed class BookingIndexProvider : IndexProvider<Booking>
{
    public BookingIndexProvider()
    {
        CollectionName = BookingsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Booking> context)
        => context.For<BookingIndex>()
            .Map(booking => new BookingIndex
            {
                ItemId = booking.ItemId,
                OrderId = booking.OrderId,
                ContentItemId = booking.ContentItemId,
                CartId = booking.CartId,
                Title = booking.Title?.Length > 255 ? booking.Title[..255] : booking.Title,
                CustomerName = booking.CustomerName?.Length > 255 ? booking.CustomerName[..255] : booking.CustomerName,
                StartsUtc = booking.StartsUtc,
                Status = booking.Status,
                HoldExpiresUtc = booking.HoldExpiresUtc,
                CreatedUtc = booking.CreatedUtc,
                UserId = booking.UserId?.Length > 26 ? booking.UserId[..26] : booking.UserId,
                CustomerEmail = BookingIndex.NormalizeEmail(booking.CustomerEmail),
            });
}

public sealed class BookingBalanceIndexProvider : IndexProvider<BookingBalance>
{
    public BookingBalanceIndexProvider()
    {
        CollectionName = BookingsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<BookingBalance> context)
        => context.For<BookingBalanceIndex>()
            .Map(balance => new BookingBalanceIndex
            {
                ItemId = balance.ItemId,
                OrderId = balance.OrderId,
                Status = balance.Status,
                DueUtc = balance.DueUtc,
                NextAttemptUtc = balance.NextAttemptUtc,
            });
}
