using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Bookings.Services;

public interface IBookingSlotStore : ICatalog<BookingSlot>
{
    Task<BookingSlot> FindAsync(string contentItemId, string slotKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, IEnumerable<string> slotKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a product's dates that start in a range, the earliest first. Without a product, every product's.
    /// </summary>
    Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

public sealed class BookingSlotStore : DocumentCatalog<BookingSlot, BookingSlotIndex>, IBookingSlotStore
{
    // Two checkouts holding the last seats must not overwrite each other's counts.
    protected override bool CheckConcurrency => true;

    public BookingSlotStore(ISession session)
        : base(session)
    {
        CollectionName = BookingsConstants.CollectionName;
    }

    public Task<BookingSlot> FindAsync(string contentItemId, string slotKey, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(contentItemId) || string.IsNullOrEmpty(slotKey)
            ? Task.FromResult<BookingSlot>(null)
            : Session.Query<BookingSlot, BookingSlotIndex>(index => index.ContentItemId == contentItemId && index.SlotKey == slotKey, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, IEnumerable<string> slotKeys, CancellationToken cancellationToken = default)
    {
        var keys = slotKeys?.Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (string.IsNullOrEmpty(contentItemId) || keys.Length == 0)
        {
            return [];
        }

        return [.. await Session.Query<BookingSlot, BookingSlotIndex>(index => index.ContentItemId == contentItemId && index.SlotKey.IsIn(keys), collection: CollectionName).ListAsync(cancellationToken)];
    }

    public async Task<IReadOnlyList<BookingSlot>> ListAsync(string contentItemId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(contentItemId)
            ? Session.Query<BookingSlot, BookingSlotIndex>(index => index.StartsUtc >= fromUtc && index.StartsUtc < toUtc, collection: CollectionName)
            : Session.Query<BookingSlot, BookingSlotIndex>(index => index.ContentItemId == contentItemId && index.StartsUtc >= fromUtc && index.StartsUtc < toUtc, collection: CollectionName);

        return [.. await query.OrderBy(index => index.StartsUtc).Take(2000).ListAsync(cancellationToken)];
    }
}

public sealed class BookingQuery
{
    /// <summary>
    /// Gets or sets the start of a buyer's name or a product's title to find.
    /// </summary>
    public string Search { get; set; }

    public BookingStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets whether only bookings from today on are listed, the soonest first.
    /// </summary>
    public bool Upcoming { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public string ContentItemId { get; set; }
}

public interface IBookingStore : ICatalog<Booking>
{
    Task<IReadOnlyList<Booking>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the held and requested bookings of a cart's unpaid orders for a product, which count as available to that cart.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListHeldByCartAsync(string cartId, string contentItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists unpaid bookings whose hold ended before <paramref name="utcNow"/>.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListExpiredHoldsAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists requests the store hasn't answered yet.
    /// </summary>
    Task<IReadOnlyList<Booking>> ListRequestedAsync(int take, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<Booking> Bookings)> PageAsync(BookingQuery query, DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class BookingStore : DocumentCatalog<Booking, BookingIndex>, IBookingStore
{
    private readonly IServiceProvider _serviceProvider;

    private IBookingHandler[] _handlers;

    public BookingStore(ISession session, IServiceProvider serviceProvider)
        : base(session)
    {
        CollectionName = BookingsConstants.CollectionName;
        _serviceProvider = serviceProvider;
    }

    // The handlers are resolved on first use, so a handler may depend on services that depend on this store.
    private IBookingHandler[] Handlers => _handlers ??= [.. _serviceProvider.GetServices<IBookingHandler>()];

    protected override async ValueTask SavingAsync(Booking record)
    {
        var known = record.TryGet<BookingChangeMarker>(out var marker);
        var context = new BookingSavingContext
        {
            Booking = record,

            // A booking saved before its changes were followed has no marker, but it isn't new once it was changed after being made.
            IsNew = !known && record.ModifiedUtc - record.CreatedUtc < TimeSpan.FromSeconds(1),
            PreviousStatus = known ? marker.Status : null,
            PreviousStartsUtc = known ? marker.StartsUtc : null,
            PreviousEndsUtc = known ? marker.EndsUtc : null,
        };

        foreach (var handler in Handlers)
        {
            await handler.SavingAsync(context);
        }

        record.Put(new BookingChangeMarker
        {
            Status = record.Status,
            StartsUtc = record.StartsUtc,
            EndsUtc = record.EndsUtc,
        });
    }

    protected override async ValueTask DeletingAsync(Booking model)
    {
        foreach (var handler in Handlers)
        {
            await handler.DeletingAsync(model);
        }
    }

    public async Task<IReadOnlyList<Booking>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(orderId)
            ? []
            : [.. await Session.Query<Booking, BookingIndex>(index => index.OrderId == orderId, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Booking>> ListHeldByCartAsync(string cartId, string contentItemId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(cartId) || string.IsNullOrEmpty(contentItemId)
            ? []
            : [.. await Session.Query<Booking, BookingIndex>(index => index.CartId == cartId && index.ContentItemId == contentItemId && (index.Status == BookingStatus.Held || index.Status == BookingStatus.Requested), collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Booking>> ListExpiredHoldsAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Booking, BookingIndex>(index => index.Status == BookingStatus.Held && index.HoldExpiresUtc != null && index.HoldExpiresUtc < utcNow, collection: CollectionName)
            .OrderBy(index => index.HoldExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Booking>> ListRequestedAsync(int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Booking, BookingIndex>(index => index.Status == BookingStatus.Requested, collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)];

    public async Task<(int Count, IReadOnlyList<Booking> Bookings)> PageAsync(BookingQuery query, DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new BookingQuery();

        var bookings = Session.Query<Booking, BookingIndex>(collection: CollectionName);

        if (query.Status is { } status)
        {
            bookings = bookings.Where(index => index.Status == status);
        }
        else
        {
            // An order that was never paid isn't a booking anyone needs to see.
            bookings = bookings.Where(index => index.Status != BookingStatus.Held && index.Status != BookingStatus.Expired);
        }

        if (!string.IsNullOrEmpty(query.ContentItemId))
        {
            bookings = bookings.Where(index => index.ContentItemId == query.ContentItemId);
        }

        if (query.FromUtc is { } from)
        {
            bookings = bookings.Where(index => index.StartsUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            bookings = bookings.Where(index => index.StartsUtc < to);
        }

        if (query.Upcoming)
        {
            var today = utcNow.Date;

            bookings = bookings.Where(index => index.StartsUtc >= today);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            bookings = bookings.Where(index => index.CustomerName.StartsWith(search) || index.Title.StartsWith(search));
        }

        var count = await bookings.CountAsync(cancellationToken);

        var page = query.Upcoming || query.FromUtc is not null
            ? await bookings.OrderBy(index => index.StartsUtc).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken)
            : await bookings.OrderByDescending(index => index.StartsUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}

public interface IBookingBalanceStore : ICatalog<BookingBalance>
{
    Task<BookingBalance> FindByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists balances to charge now: scheduled, and due, or due to be tried again.
    /// </summary>
    Task<IReadOnlyList<BookingBalance>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);
}

public sealed class BookingBalanceStore : DocumentCatalog<BookingBalance, BookingBalanceIndex>, IBookingBalanceStore
{
    public BookingBalanceStore(ISession session)
        : base(session)
    {
        CollectionName = BookingsConstants.CollectionName;
    }

    public Task<BookingBalance> FindByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(orderId)
            ? Task.FromResult<BookingBalance>(null)
            : Session.Query<BookingBalance, BookingBalanceIndex>(index => index.OrderId == orderId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingBalance>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<BookingBalance, BookingBalanceIndex>(index =>
                index.Status == BookingBalanceStatus.Scheduled && index.DueUtc <= utcNow && (index.NextAttemptUtc == null || index.NextAttemptUtc <= utcNow),
                collection: CollectionName)
            .OrderBy(index => index.DueUtc)
            .Take(take)
            .ListAsync(cancellationToken)];
}
