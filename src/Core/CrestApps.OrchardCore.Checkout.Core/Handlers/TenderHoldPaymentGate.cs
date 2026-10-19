using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Holds what each balance pays toward the checkout when payment begins, and refuses the payment when one has less
/// on it than the invoice counted on, rebuilding the invoice so the buyer sees what is left to pay.
/// </summary>
public sealed class TenderHoldPaymentGate : ICheckoutPaymentGate
{
    private readonly ICheckoutTenderLedger _ledger;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    internal readonly IStringLocalizer S;

    public TenderHoldPaymentGate(
        ICheckoutTenderLedger ledger,
        CheckoutInvoiceBuilder invoiceBuilder,
        IStringLocalizer<TenderHoldPaymentGate> stringLocalizer)
    {
        _ledger = ledger;
        _invoiceBuilder = invoiceBuilder;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = context.Flow.Session;
        var tenders = session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.Tenders ?? [] : [];

        // A balance the buyer took off since an earlier attempt to pay is let go.
        await _ledger.ReleaseOthersAsync(session.SessionId, tenders, cancellationToken);

        foreach (var tender in tenders.ToArray())
        {
            var availability = await _ledger.HoldAsync(context.Flow, tender, cancellationToken);

            if (availability == TenderAvailability.Available)
            {
                continue;
            }

            if (availability == TenderAvailability.Busy)
            {
                context.Refuse(S["We couldn't reserve {0} just now. Please try again.", tender.Description]);

                return;
            }

            // One that can't pay at all comes off; one with less on it stays, and the rebuilt invoice uses what's left.
            if (availability == TenderAvailability.Unavailable && session.TryGet<CheckoutTenderSelection>(out var selection))
            {
                selection.Remove(tender.ProviderKey, tender.Reference);
                session.Put(selection);
            }

            await _invoiceBuilder.BuildAsync(context.Flow);

            context.InvoiceChanged = true;
            context.Refuse(availability == TenderAvailability.Insufficient
                ? S["{0} has less on it than when it was applied, so it pays less. Check what's left to pay, then pay again.", tender.Description]
                : S["{0} can't be used any more, so it was taken off. Check what's left to pay, then pay again.", tender.Description]);

            return;
        }
    }
}
