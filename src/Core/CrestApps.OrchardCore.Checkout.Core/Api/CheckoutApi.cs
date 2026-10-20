using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Api;

/// <summary>
/// What every checkout endpoint shares, whichever module maps it: finding the caller's checkout, and reading it into
/// <see cref="ApiCheckout"/>.
/// </summary>
public static class CheckoutApi
{
    /// <summary>
    /// The tag the checkout endpoints are documented under.
    /// </summary>
    public const string Tag = "Checkout";

    /// <summary>
    /// The key the API names the payment step by, in <see cref="ApiCheckout.NextStep"/>.
    /// </summary>
    public const string PaymentStep = CheckoutConstants.PaymentStepKey;

    private static readonly CheckoutSessionStatus[] _open =
    [
        CheckoutSessionStatus.Pending,
        CheckoutSessionStatus.AwaitingProvider,
        CheckoutSessionStatus.PaymentPending,
    ];

    private static readonly CheckoutSessionStatus[] _any =
    [
        .. _open,
        CheckoutSessionStatus.Completed,
        CheckoutSessionStatus.Failed,
        CheckoutSessionStatus.Canceled,
        CheckoutSessionStatus.Expired,
    ];

    /// <summary>
    /// Finds a checkout of the caller's that can still be changed. The store checks it's theirs: the signed-in owner's,
    /// or the guest's whose <c>X-Checkout-Token</c> it is. Anybody else's answers as missing.
    /// </summary>
    public static Task<CheckoutSession> FindOpenAsync(ICheckoutSessionStore store, string id, CancellationToken cancellationToken)
        => FindAsync(store, id, _open, cancellationToken);

    /// <summary>
    /// Finds a checkout of the caller's, finished or not.
    /// </summary>
    public static Task<CheckoutSession> FindAnyAsync(ICheckoutSessionStore store, string id, CancellationToken cancellationToken)
        => FindAsync(store, id, _any, cancellationToken);

    /// <summary>
    /// Returns whether a step is done: saved, or one that asks for nothing.
    /// </summary>
    public static bool IsDone(CheckoutSession session, CheckoutFlowStep step)
        => string.Equals(step.Key, PaymentStep, StringComparison.Ordinal)
            ? session.Status == CheckoutSessionStatus.Completed
            : !step.CollectData || session.SavedSteps.ContainsKey(step.Key);

    /// <summary>
    /// Reads a checkout as the API returns it.
    /// </summary>
    public static ApiCheckout Read(CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var flow = new CheckoutFlow(session);
        var invoice = session.TryGet<CheckoutInvoice>(out var found) ? found : null;
        var currency = invoice?.Currency ?? session.Currency;
        var steps = flow.GetSortedSteps();
        var apiSteps = steps.Select(step => new ApiCheckoutStep(step.Key, step.Title, IsDone(session, step))).ToList();
        var nextStep = steps.FirstOrDefault(step => !IsDone(session, step) && !string.Equals(step.Key, PaymentStep, StringComparison.Ordinal))?.Key;

        if (nextStep is null && session.Status is CheckoutSessionStatus.Pending or CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            nextStep = PaymentStep;
        }

        var contact = session.TryGet<CheckoutContactInfo>(out var contactInfo)
            ? new ApiCheckoutContact(contactInfo.DisplayName, contactInfo.Email, contactInfo.Phone, ApiAddress.From(contactInfo.BillingAddress))
            : null;

        var shipping = session.TryGet<CheckoutShippingInfo>(out var shippingInfo) && !string.IsNullOrEmpty(shippingInfo.RateId)
            ? new ApiCheckoutShipping(shippingInfo.RateId, shippingInfo.Name, shippingInfo.Description, ApiMoney.From(shippingInfo.Amount, currency), ApiAddress.From(shippingInfo.Address))
            : null;

        var lines = (invoice?.LineItems ?? [])
            .Select(line => new ApiCheckoutLine(
                line.ItemId,
                line.ProductId,
                line.Sku,
                line.Description,
                line.Quantity,
                ApiMoney.From(line.UnitPrice, currency),
                ApiMoney.From(line.DiscountAmount, currency),
                ApiMoney.From(line.GetLineTotal(currency), currency),
                line.Plan is null ? null : ToInterval(line.Plan.DurationType),
                line.Plan?.BillingDuration))
            .ToList();

        // Shipping is a line of the invoice, so it's taxed and discounted like one; the totals give it on its own.
        var subtotal = (invoice?.LineItems ?? []).Where(line => line.TaxableKind != TaxableItemKind.Shipping).Sum(line => line.UnitPrice * line.Quantity);
        var shippingTotal = (invoice?.LineItems ?? []).Where(line => line.TaxableKind == TaxableItemKind.Shipping).Sum(line => line.UnitPrice * line.Quantity);

        return new ApiCheckout(
            session.SessionId,
            ToCamel(session.Status.ToString()),
            string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ? session.ReferenceId : null,
            GetNumber(session),
            currency,
            apiSteps,
            nextStep,
            contact,
            shipping,
            lines,
            (invoice?.Discounts ?? [])
                .Select(discount => new ApiCheckoutDiscount(discount.Code, discount.Description, ApiMoney.From(discount.Amount, currency), ToCamel(discount.Target.ToString()), discount.IsAutomatic))
                .ToList(),
            CheckoutCouponService.GetCodes(flow),
            (invoice?.Tenders ?? [])
                .Select(tender => new ApiCheckoutTender(tender.ProviderKey, tender.Reference, tender.Description, ApiMoney.From(tender.Amount, currency)))
                .ToList(),
            invoice is null
                ? null
                : new ApiCheckoutTotals(
                    ApiMoney.From(subtotal, currency),
                    ApiMoney.From(shippingTotal, currency),
                    ApiMoney.From(invoice.DiscountTotal, currency),
                    ApiMoney.From(invoice.TaxAmount, currency),
                    ApiMoney.From(invoice.GrandTotal, currency),
                    ApiMoney.From(invoice.TenderTotal, currency),
                    ApiMoney.From(invoice.AmountDue, currency)),
            session.CompletedUtc);
    }

    /// <summary>
    /// Reads where completing a checkout got to.
    /// </summary>
    public static ApiCheckoutCompletion Read(CheckoutCompletionResult result, CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ApiCheckoutCompletion(
            ToCamel(result.Status.ToString()),
            result.IsCompleted,
            result.BlockingStepKey,
            result.ErrorMessage,
            session is not null && string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ? session.ReferenceId : null,
            session is null ? null : GetNumber(session));
    }

    private static string GetNumber(CheckoutSession session)
        => session.TryGet<CheckoutReferenceNumber>(out var reference) ? reference.Number : null;

    internal static string ToInterval(DurationType type)
        => type switch
        {
            DurationType.Day => "day",
            DurationType.Week => "week",
            DurationType.Month => "month",
            _ => "year",
        };

    internal static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static async Task<CheckoutSession> FindAsync(ICheckoutSessionStore store, string id, CheckoutSessionStatus[] statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var status in statuses)
        {
            if (await store.GetAsync(id, status, cancellationToken) is { } session)
            {
                return session;
            }
        }

        return null;
    }
}
