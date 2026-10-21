using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Exports the coupons a person redeemed and anonymizes the redemptions.
/// </summary>
/// <remarks>
/// A coupon keeps only a redemption count. Who redeemed it is recorded on the completed checkout, as a discount line of
/// its invoice and the code the buyer entered, so a person's redemptions are the coupon discounts of their completed
/// checkouts. Anonymizing them removes the person's emails and names from those entries; the code, amount and session
/// stay, so the coupon's redemption count and the invoice still add up.
/// </remarks>
public sealed class CouponPersonalDataContributor : ICommercePersonalDataContributor
{
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICouponRedemptionService _redemptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store.</param>
    /// <param name="redemptions">The coupon uses, which forget who used them.</param>
    public CouponPersonalDataContributor(ICheckoutSessionStore sessionStore, ICouponRedemptionService redemptions)
    {
        _sessionStore = sessionStore;
        _redemptions = redemptions;
    }

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var (session, invoice, discount) in await GetRedemptionsAsync(context.Subject, cancellationToken))
        {
            context.Export.CouponRedemptions.Add(new CommerceCouponRedemptionRecord
            {
                CheckoutSessionId = session.SessionId,
                Code = discount.Code,
                Description = discount.Description,
                Currency = invoice.Currency ?? session.Currency,
                Amount = discount.Amount,
                RedeemedUtc = session.CompletedUtc,
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var group in (await GetRedemptionsAsync(context.Subject, cancellationToken)).GroupBy(redemption => redemption.Session))
        {
            var session = group.Key;
            var invoice = group.First().Invoice;
            var changed = false;

            foreach (var (_, _, discount) in group)
            {
                if (context.Subject.IsMentionedIn(discount.Description))
                {
                    discount.Description = context.Scrub(discount.Description);
                    changed = true;
                }

                context.Result.CouponRedemptions++;
            }

            if (changed)
            {
                session.Put(invoice);

                await _sessionStore.SaveAsync(session, cancellationToken);
            }
        }

        // The uses keep their code, amount and checkout for the coupon's count; who used them is forgotten.
        await _redemptions.ForgetCustomersAsync(context.Subject.CheckoutSessionIds, cancellationToken);
    }

    private async Task<List<(CheckoutSession Session, CheckoutInvoice Invoice, DiscountLine Discount)>> GetRedemptionsAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var redemptions = new List<(CheckoutSession Session, CheckoutInvoice Invoice, DiscountLine Discount)>();

        foreach (var batch in subject.CheckoutSessionIds.Order(StringComparer.Ordinal).Chunk(CheckoutPersonalDataContributor.BatchSize))
        {
            foreach (var session in await _sessionStore.GetManyAsync(batch, cancellationToken))
            {
                // A coupon is redeemed when the checkout completes, so the discounts of an unfinished checkout were
                // never redemptions.
                if (session.Status != CheckoutSessionStatus.Completed ||
                    !session.TryGet<CheckoutInvoice>(out var invoice) ||
                    invoice?.Discounts is not { Count: > 0 })
                {
                    continue;
                }

                foreach (var discount in invoice.Discounts)
                {
                    if (string.Equals(discount.ProviderKey, CouponDiscountProvider.ProviderKey, StringComparison.Ordinal))
                    {
                        redemptions.Add((session, invoice, discount));
                    }
                }
            }
        }

        return redemptions;
    }
}
