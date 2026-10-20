using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// A booking's life: made from its order line, holding its dates while the order is unpaid, confirmed when it's paid,
/// then completed, a no-show, or canceled under its cancellation policy with the refund that policy gives.
/// </summary>
public sealed class BookingService
{
    // How long an unpaid order holds its dates, as long as stock is held for one.
    internal static readonly TimeSpan HoldTime = TimeSpan.FromMinutes(60);

    private readonly IBookingStore _bookings;
    private readonly IBookingBalanceStore _balances;
    private readonly BookingCapacityService _capacity;
    private readonly IContentManager _contentManager;
    private readonly IOrderStore _orders;
    private readonly IOrderService _orderService;
    private readonly BookingEmailSender _emails;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public BookingService(
        IBookingStore bookings,
        IBookingBalanceStore balances,
        BookingCapacityService capacity,
        IContentManager contentManager,
        IOrderStore orders,
        IOrderService orderService,
        BookingEmailSender emails,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<BookingService> logger,
        IStringLocalizer<BookingService> stringLocalizer)
    {
        _bookings = bookings;
        _balances = balances;
        _capacity = capacity;
        _contentManager = contentManager;
        _orders = orders;
        _orderService = orderService;
        _emails = emails;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Makes a booking of each of the order's booking lines and holds their dates, all or none. Returns why it couldn't,
    /// or <see langword="null"/>.
    /// </summary>
    public async Task<string> BookAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var made = new List<Booking>();

        foreach (var line in order.Lines ?? [])
        {
            if (line.Details is not { } details || !details.ContainsKey(BookingsConstants.Details.Date))
            {
                continue;
            }

            var item = await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Published);

            if (item?.TryGet<BookingPart>(out var part) != true)
            {
                continue;
            }

            // A booking the store confirms first is only placed as a request.
            if (part.RequestToBook && order.Quote is null)
            {
                await ReleaseAsync(made, cancellationToken);

                return S["The store confirms {0} before it's paid. Choose Request to book on your cart.", line.Title];
            }

            var booking = await NewAsync(order, line, part, cancellationToken);

            if (booking is null || !await _capacity.HoldAsync(booking, part, cancellationToken))
            {
                await ReleaseAsync(made, cancellationToken);

                return S["{0} is no longer available then. Choose another date.", line.Title];
            }

            made.Add(booking);
        }

        foreach (var booking in made)
        {
            await _bookings.CreateAsync(booking, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Confirms an order's bookings once it's paid: their dates are taken, and the buyer is told.
    /// </summary>
    public async Task ConfirmPaidAsync(Order order, CancellationToken cancellationToken = default)
    {
        var bookings = (await _bookings.ListByOrderAsync(order.ItemId, cancellationToken))
            .Where(booking => booking.Status is BookingStatus.Held or BookingStatus.Requested or BookingStatus.Expired)
            .ToList();

        if (bookings.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var booking in bookings)
        {
            // An order paid after its hold ran out takes its dates again; if they've gone since, it's still booked and the
            // store sorts it out.
            if (booking.Status == BookingStatus.Expired)
            {
                var item = await _contentManager.GetAsync(booking.ContentItemId, VersionOptions.Published);

                if (item?.TryGet<BookingPart>(out var part) != true || !await _capacity.HoldAsync(booking, part, cancellationToken))
                {
                    _logger.LogWarning("Order '{OrderNumber}' was paid after its hold on '{Title}' ended, and those dates are full now. It's booked over capacity.", order.OrderNumber, booking.Title);
                    await _orderService.AddNoteAsync(order, S["{0} was paid after its hold ended, and its dates are full now. It's booked over capacity.", booking.Title], OrderNoteKind.Event, authorName: null, cancellationToken);

                    booking.Status = BookingStatus.Confirmed;
                    booking.ConfirmedUtc = now;
                    booking.ModifiedUtc = now;
                    await _bookings.UpdateAsync(booking, cancellationToken);

                    continue;
                }
            }

            await _capacity.CommitAsync(booking, cancellationToken);

            booking.Status = BookingStatus.Confirmed;
            booking.HoldExpiresUtc = null;
            booking.ConfirmedUtc = now;
            booking.ModifiedUtc = now;
            booking.CustomerName = order.CustomerName ?? booking.CustomerName;
            booking.CustomerEmail = order.CustomerEmail ?? booking.CustomerEmail;
            booking.UserId = order.UserId ?? booking.UserId;

            await _bookings.UpdateAsync(booking, cancellationToken);
        }

        var balance = await _balances.FindByOrderAsync(order.ItemId, cancellationToken);

        await _emails.SendConfirmedAsync(order, bookings, balance, await _capacity.GetTimeZoneAsync(), cancellationToken);
    }

    /// <summary>
    /// Gives back the dates of an order's bookings that aren't done, when the order is canceled or its lines change.
    /// </summary>
    public async Task ReleaseOrderAsync(Order order, BookingStatus status, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        foreach (var booking in await _bookings.ListByOrderAsync(order.ItemId, cancellationToken))
        {
            if (booking.Status is not (BookingStatus.Held or BookingStatus.Requested or BookingStatus.Confirmed))
            {
                continue;
            }

            await _capacity.ReleaseAsync(booking, sold: booking.Status == BookingStatus.Confirmed, cancellationToken);

            booking.Status = status;
            booking.HoldExpiresUtc = null;
            booking.ModifiedUtc = now;

            if (status == BookingStatus.Canceled)
            {
                booking.CanceledUtc = now;
            }

            await _bookings.UpdateAsync(booking, cancellationToken);
        }
    }

    /// <summary>
    /// Removes an order's bookings that were only held, so its changed lines can be booked again.
    /// </summary>
    public async Task RebookAsync(Order order, Action<string> refuse, CancellationToken cancellationToken = default)
    {
        foreach (var booking in await _bookings.ListByOrderAsync(order.ItemId, cancellationToken))
        {
            if (booking.Status is BookingStatus.Held or BookingStatus.Requested)
            {
                await _capacity.ReleaseAsync(booking, sold: false, cancellationToken);
                await _bookings.DeleteAsync(booking, cancellationToken);
            }
        }

        if (await BookAsync(order, cancellationToken) is { } problem)
        {
            refuse(problem);
        }
    }

    /// <summary>
    /// Gives back the dates of unpaid orders whose hold ran out.
    /// </summary>
    public async Task<int> ReleaseExpiredAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var expired = await _bookings.ListExpiredHoldsAsync(now, take, cancellationToken);

        foreach (var booking in expired)
        {
            await _capacity.ReleaseAsync(booking, sold: false, cancellationToken);

            booking.Status = BookingStatus.Expired;
            booking.HoldExpiresUtc = null;
            booking.ModifiedUtc = now;

            await _bookings.UpdateAsync(booking, cancellationToken);
        }

        // A request whose quote was turned down, or ran out, gives its dates back too.
        foreach (var booking in await _bookings.ListRequestedAsync(take, cancellationToken))
        {
            var order = await _orders.FindByIdAsync(booking.OrderId, cancellationToken);

            if (order is null ||
                order.Status == OrderStatus.Canceled ||
                order.Quote is { Status: OrderQuoteStatus.Declined } ||
                order.Quote is { ExpiresUtc: { } expires } && expires < now && order.Status != OrderStatus.Paid)
            {
                await _capacity.ReleaseAsync(booking, sold: false, cancellationToken);

                booking.Status = BookingStatus.Expired;
                booking.ModifiedUtc = now;

                await _bookings.UpdateAsync(booking, cancellationToken);
            }
        }

        return expired.Count;
    }

    /// <summary>
    /// Marks a confirmed booking as done: completed, or a no-show.
    /// </summary>
    public async Task<bool> FinishAsync(Booking booking, bool noShow, string actorName, CancellationToken cancellationToken = default)
    {
        if (booking?.Status != BookingStatus.Confirmed)
        {
            return false;
        }

        var now = _clock.UtcNow;

        booking.Status = noShow ? BookingStatus.NoShow : BookingStatus.Completed;
        booking.CompletedUtc = now;
        booking.ModifiedUtc = now;

        await _bookings.UpdateAsync(booking, cancellationToken);

        if (await _orders.FindByIdAsync(booking.OrderId, cancellationToken) is { } order)
        {
            await _orderService.AddNoteAsync(order, noShow ? S["{0}: marked as a no-show.", booking.Title] : S["{0}: completed.", booking.Title], OrderNoteKind.Event, actorName, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Works out what canceling a booking now gives back under its policy: the fee's percent, what was paid toward it
    /// and the refund.
    /// </summary>
    public async Task<BookingCancellationQuote> QuoteCancellationAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var order = await _orders.FindByIdAsync(booking.OrderId, cancellationToken);
        var timeZone = await _capacity.GetTimeZoneAsync();
        var today = DateOnly.FromDateTime(BookingEmailSender.ToLocal(_clock.UtcNow, timeZone));
        var start = DateOnly.FromDateTime(BookingEmailSender.ToLocal(booking.StartsUtc, timeZone));
        var daysBefore = start.DayNumber - today.DayNumber;
        var feePercent = BookingSchedule.GetCancellationFeePercent(booking.CancellationTiers, daysBefore);

        // What was paid toward this booking: its share of what can still be refunded for the order.
        var refundable = 0m;

        if (order is not null && _serviceProvider.GetService<IOrderRefunder>() is { } refunder)
        {
            var (orderRefundable, _) = await refunder.GetMoneyAsync(order, cancellationToken);

            // What the balance put on the ledger counts as refundable, since it can be let go of, but it was never paid.
            var deferred = (order.Tenders ?? []).Where(tender => tender.ProviderKey == BookingsConstants.BalanceTenderKey).Sum(tender => tender.Amount);
            var paid = Math.Max(0m, orderRefundable - deferred);
            var orderTotal = order.Total ?? order.Subtotal;

            refundable = orderTotal > 0m ? Money.Round(paid * Math.Min(1m, booking.Total / orderTotal), booking.Currency) : paid;
        }

        return new BookingCancellationQuote(daysBefore, feePercent, refundable, BookingSchedule.GetRefund(booking.Total, refundable, feePercent));
    }

    /// <summary>
    /// Cancels a booking: refunds what its policy gives back (or <paramref name="refund"/>, when staff choose another
    /// amount), lets go of what's still owed on it, gives its dates back, and tells the buyer.
    /// </summary>
    public async Task<(bool Succeeded, IReadOnlyList<string> Errors)> CancelAsync(Booking booking, decimal? refund, string reason, string actorName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Requested or BookingStatus.Held))
        {
            return (false, [S["This booking can't be canceled any more."]]);
        }

        var order = await _orders.FindByIdAsync(booking.OrderId, cancellationToken);
        var quote = await QuoteCancellationAsync(booking, cancellationToken);
        var amount = Math.Clamp(refund ?? quote.Refund, 0m, quote.Paid);
        var refunded = 0m;

        if (order is not null && amount > 0m)
        {
            if (_serviceProvider.GetService<IOrderRefunder>() is not { } refunder)
            {
                return (false, [S["Refunds need the Orders feature."]]);
            }

            var (done, errors) = await refunder.RefundAsync(order, amount, string.IsNullOrWhiteSpace(reason) ? S["{0} canceled", booking.Title].Value : reason, actorName, cancellationToken);

            if (errors.Count > 0 && done <= 0m)
            {
                return (false, errors);
            }

            refunded = done;

            // A refund commits what it changed, which detaches what was read before it: the booking is read again, so
            // it's updated rather than stored twice.
            booking = await _bookings.FindByIdAsync(booking.ItemId, cancellationToken) ?? booking;
        }

        // What's still owed on it isn't any more.
        if (order is not null)
        {
            await CancelBalanceAsync(order, S["The booking was canceled."].Value, cancellationToken);
        }

        await _capacity.ReleaseAsync(booking, sold: booking.Status == BookingStatus.Confirmed, cancellationToken);

        var now = _clock.UtcNow;

        booking.Status = BookingStatus.Canceled;
        booking.CanceledUtc = now;
        booking.ModifiedUtc = now;
        booking.CancellationFee = Money.Round(booking.Total * quote.FeePercent / 100m, booking.Currency);
        booking.Refunded = refunded;
        booking.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _bookings.UpdateAsync(booking, cancellationToken);

        if (order is not null)
        {
            await _orderService.AddNoteAsync(order, S["{0} canceled {1} days before, with a {2}% fee: {3} refunded.", booking.Title, quote.DaysBefore, quote.FeePercent, Products.Core.Services.ProductPriceFormatter.Format(refunded, booking.Currency)], OrderNoteKind.Event, actorName, cancellationToken);
            await _emails.SendCanceledAsync(order, booking, refunded, await _capacity.GetTimeZoneAsync(), cancellationToken);
        }

        return (true, []);
    }

    // Lets go of an order's balance: it's not charged, and what's owed on its invoice is waived.
    private async Task CancelBalanceAsync(Order order, string reason, CancellationToken cancellationToken)
    {
        if (await _balances.FindByOrderAsync(order.ItemId, cancellationToken) is not { Status: BookingBalanceStatus.Scheduled or BookingBalanceStatus.Failed } balance)
        {
            return;
        }

        balance.Status = BookingBalanceStatus.Canceled;
        await _balances.UpdateAsync(balance, cancellationToken);

        if (_serviceProvider.GetService<ITransactionManager>() is { } transactions &&
            _serviceProvider.GetService<ITransactionCommandService>() is { } commands &&
            await transactions.FindByIdAsync(balance.TransactionId, cancellationToken) is { OutstandingAmount: > 0m } transaction)
        {
            var result = await commands.AdjustAsync(transaction.ItemId, transaction.OutstandingAmount, TransactionAdjustmentKind.Waiver, reason, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The balance of order '{OrderNumber}' couldn't be waived: {Errors}", order.OrderNumber, string.Join(" ", result.Errors));
            }
        }
    }

    private async Task<Booking> NewAsync(Order order, OrderLine line, BookingPart part, CancellationToken cancellationToken)
    {
        line.Details.TryGetValue(BookingsConstants.Details.End, out var end);

        var keys = BookingSchedule.GetSlotKeys(part.Kind, line.Details[BookingsConstants.Details.Date], end);

        if (keys.Count == 0)
        {
            return null;
        }

        var timeZone = await _capacity.GetTimeZoneAsync();
        var occupancy = BookingCartLineHandler.Find(part, line.Details.GetValueOrDefault(BookingsConstants.Details.Occupancy));
        var people = Math.Max(1, occupancy?.People ?? 1);
        var units = Math.Max(1, line.Quantity);
        var startsUtc = BookingSchedule.GetStartUtc(keys[0], timeZone).Value;
        var now = _clock.UtcNow;

        return new Booking
        {
            ItemId = global::OrchardCore.IdGenerator.GenerateId(),
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            LineId = line.LineId,
            // An order without a cart, such as a request, is its own: re-pricing it counts what it holds as available to it.
            CartId = order.CartId ?? order.ItemId,
            ContentItemId = line.ContentItemId,
            Title = line.Title,
            Kind = part.Kind,
            SlotKeys = [.. keys],
            StartsUtc = startsUtc,
            EndsUtc = part.Kind switch
            {
                BookingKind.Nightly => BookingSchedule.GetStartUtc(BookingSchedule.ToKey(DateOnly.FromDateTime(BookingSchedule.GetLocalStart(keys[^1]).Value).AddDays(1)), timeZone),
                BookingKind.TimeSlot => startsUtc.AddMinutes(Math.Max(5, part.SlotMinutes)),
                _ => null,
            },
            Units = units,
            People = units * people,
            Seats = units * BookingSchedule.GetSeatsPerUnit(part.Kind, people),
            OccupancyLabel = occupancy?.Label,
            Status = order.Quote is null ? BookingStatus.Held : BookingStatus.Requested,

            // A request is held until the store answers it; an unpaid order for as long as stock would be.
            HoldExpiresUtc = order.Quote is null ? now.Add(HoldTime) : null,
            UserId = order.UserId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Total = line.LineTotal,
            Currency = order.Currency,
            CancellationTiers = [.. (part.CancellationTiers ?? []).Select(tier => new BookingCancellationTier { DaysBefore = tier.DaysBefore, FeePercent = tier.FeePercent })],
            CreatedUtc = now,
            ModifiedUtc = now,
        };
    }

    private async Task ReleaseAsync(IEnumerable<Booking> bookings, CancellationToken cancellationToken)
    {
        foreach (var booking in bookings)
        {
            await _capacity.ReleaseAsync(booking, sold: false, cancellationToken);
        }
    }
}

/// <summary>
/// What canceling a booking now would cost: how many days before its date it is, the fee's percent under its policy,
/// what was paid toward it, and what's refunded.
/// </summary>
public sealed record BookingCancellationQuote(int DaysBefore, decimal FeePercent, decimal Paid, decimal Refund);
