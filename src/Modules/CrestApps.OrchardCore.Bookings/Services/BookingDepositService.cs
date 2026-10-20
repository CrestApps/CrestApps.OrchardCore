using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// What a checkout's bookings let the buyer pay now: a deposit of each booking whose product takes one, the rest owed
/// on a date some days before the earliest of them and charged then to the card the deposit is paid with.
/// </summary>
public sealed class BookingDepositService
{
    private readonly IOrderStore _orders;
    private readonly IContentManager _contentManager;
    private readonly BookingCapacityService _capacity;

    public BookingDepositService(IOrderStore orders, IContentManager contentManager, BookingCapacityService capacity)
    {
        _orders = orders;
        _contentManager = contentManager;
        _capacity = capacity;
    }

    public static string ToReference(string orderId, string sessionId)
        => orderId + ":" + sessionId;

    public static (string OrderId, string SessionId) FromReference(string reference)
    {
        var separator = reference?.IndexOf(':') ?? -1;

        return separator > 0 ? (reference[..separator], reference[(separator + 1)..]) : (null, null);
    }

    /// <summary>
    /// Returns the order a checkout pays, when it's an order whose bookings can be paid by deposit.
    /// </summary>
    public async Task<Order> GetOrderAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
        => flow?.Session is CheckoutSession { SettlesExistingBalance: false, OwnerId.Length: > 0 } session &&
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !session.TryGet<CheckoutCounterSale>(out _)
                ? await _orders.FindByIdAsync(session.ReferenceId, cancellationToken)
                : null;

    /// <summary>
    /// Returns the deposit the order's bookings take, or <see langword="null"/> when they're paid in full: none takes a
    /// deposit, or the balance would already be due.
    /// </summary>
    public async Task<BookingDepositOffer> GetOfferAsync(Order order, CheckoutInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (order is null || order.Status == OrderStatus.Paid || order.Channel == OrderChannels.PointOfSale || invoice?.InitialPaymentAmount is not > 0m)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(await _capacity.GetLocalNowAsync());
        var balance = 0m;
        var percent = 0m;
        DateOnly? due = null;

        foreach (var line in order.Lines ?? [])
        {
            if (line.Details?.GetValueOrDefault(BookingsConstants.Details.Date) is not { } date ||
                BookingSchedule.GetLocalStart(date) is not { } start ||
                (await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Published))?.TryGet<BookingPart>(out var part) != true ||
                part.DepositPercent is <= 0m or >= 100m)
            {
                continue;
            }

            var lineDue = BookingSchedule.GetBalanceDueDate(DateOnly.FromDateTime(start), part.BalanceDueDaysBefore);

            // A booking whose balance would be due already is paid in full.
            if (lineDue <= today)
            {
                continue;
            }

            balance += line.LineTotal * (100m - part.DepositPercent) / 100m;
            percent = Math.Max(percent, part.DepositPercent);
            due = due is null || lineDue < due ? lineDue : due;
        }

        balance = Money.Round(balance, order.Currency);

        // Something is always paid now, so the card is there to keep.
        balance = Math.Min(balance, Money.Round(invoice.InitialPaymentAmount.Value - 1m, order.Currency));

        if (balance <= 0m || due is null)
        {
            return null;
        }

        var dueUtc = TimeZoneInfo.ConvertTimeToUtc(due.Value.ToDateTime(new TimeOnly(9, 0)), await _capacity.GetTimeZoneAsync());

        return new BookingDepositOffer(balance, percent, dueUtc, due.Value);
    }
}

/// <summary>
/// A checkout's deposit: what's left to pay later, the deposit's percent, and when the rest is due.
/// </summary>
public sealed record BookingDepositOffer(decimal Balance, decimal DepositPercent, DateTime DueUtc, DateOnly DueDate);
