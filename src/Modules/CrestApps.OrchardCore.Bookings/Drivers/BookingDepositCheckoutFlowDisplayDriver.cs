using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Bookings.Drivers;

/// <summary>
/// The "Pay a deposit" box on the Payment step, for an order whose bookings take one: one click pays the deposit now,
/// keeps the card, and owes the rest on the date it's charged.
/// </summary>
internal sealed class BookingDepositCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    internal const string ApplyAction = CheckoutConstants.FormActions.ApplyTender + ":" + BookingsConstants.BalanceTenderKey;

    private readonly BookingDepositService _deposits;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutTenderChoices _choices;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    public BookingDepositCheckoutFlowDisplayDriver(
        BookingDepositService deposits,
        IHttpContextAccessor httpContextAccessor,
        CheckoutTenderChoices choices,
        CheckoutInvoiceBuilder invoiceBuilder)
    {
        _deposits = deposits;
        _httpContextAccessor = httpContextAccessor;
        _choices = choices;
        _invoiceBuilder = invoiceBuilder;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (await _deposits.GetOrderAsync(flow) is not { } order || !flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        var tender = (invoice.Tenders ?? []).FirstOrDefault(entry => entry.ProviderKey == BookingsConstants.BalanceTenderKey);
        var whole = new CheckoutInvoice { InitialPaymentAmount = invoice.InitialPaymentAmount + (invoice.Tenders ?? []).Sum(entry => entry.Amount), Currency = invoice.Currency };
        var offer = await _deposits.GetOfferAsync(order, whole);

        if (offer is null)
        {
            return null;
        }

        return Initialize<BookingDepositCheckoutViewModel>("CheckoutBookingDeposit", model =>
        {
            model.Reference = BookingDepositService.ToReference(order.ItemId, flow.Session.SessionId);
            model.DepositPercent = offer.DepositPercent;
            model.Balance = tender?.Amount ?? offer.Balance;
            model.DueDate = offer.DueDate;
            model.Currency = invoice.Currency;
            model.IsApplied = tender is not null;
        }).Location("Aside:5");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (await _deposits.GetOrderAsync(flow) is not { } order || flow.Session is not CheckoutSession session)
        {
            return null;
        }

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var reference = BookingDepositService.ToReference(order.ItemId, session.SessionId);

        if (string.Equals(action, ApplyAction, StringComparison.Ordinal))
        {
            session.SavePaymentMethod = false;

            if (await _choices.AddAsync(flow, BookingsConstants.BalanceTenderKey, reference))
            {
                // The card the deposit is paid with is kept, to charge the rest on its date. A checkout that keeps the card
                // takes no other balance, so the invoice is built again without them.
                session.SavePaymentMethod = true;

                await _invoiceBuilder.BuildAsync(flow);
            }
        }
        else if (string.Equals(action, CheckoutConstants.FormActions.RemoveTenderPrefix + BookingsConstants.BalanceTenderKey + ":" + reference, StringComparison.Ordinal))
        {
            await _choices.RemoveAsync(flow, BookingsConstants.BalanceTenderKey, reference);

            session.SavePaymentMethod = false;

            await _invoiceBuilder.BuildAsync(flow);
        }

        return await EditStepAsync(flow, context);
    }
}

public class BookingDepositCheckoutViewModel
{
    public string Reference { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal Balance { get; set; }

    public DateOnly DueDate { get; set; }

    public string Currency { get; set; }

    public bool IsApplied { get; set; }
}
