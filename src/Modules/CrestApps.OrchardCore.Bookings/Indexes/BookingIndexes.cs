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
