using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Decides whether a coupon limited to some sources, reference types or one customer (<see cref="Coupon.IsScoped"/>)
/// may be used on a checkout. It is the one rule both the place a code is entered and the discount provider apply, so a
/// code refused at entry never takes anything off later, and a code that was accepted stops applying if the checkout
/// no longer matches.
/// </summary>
public sealed class CouponScopeEvaluator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponScopeEvaluator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider the optional customer contact resolver is resolved from, to
    /// read the email on the buyer's account when a coupon is limited to one email.</param>
    public CouponScopeEvaluator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Returns whether <paramref name="coupon"/> may be used on the checkout <paramref name="flow"/> belongs to. A coupon
    /// that is not scoped may be used anywhere. A scoped coupon is refused on a checkout that does not say what it needs
    /// to know, so a limit can never be got round by leaving something out.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="flow">The checkout.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<bool> IsInScopeAsync(Coupon coupon, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coupon);

        if (!coupon.IsScoped)
        {
            return true;
        }

        if (flow?.Session is not CheckoutSession session)
        {
            return false;
        }

        var scope = session.TryGet<CheckoutDiscountScope>(out var stored) ? stored : null;

        if (coupon.Sources?.Count > 0 && !Contains(coupon.Sources, scope?.Source))
        {
            return false;
        }

        if (coupon.ReferenceTypes?.Count > 0 &&
            !Contains(coupon.ReferenceTypes, session.ReferenceType) &&
            !Contains(coupon.ReferenceTypes, scope?.ReferenceType))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(coupon.CustomerId))
        {
            var customerId = coupon.CustomerId.Trim();

            if (!string.Equals(customerId, session.OwnerId, StringComparison.Ordinal) &&
                !string.Equals(customerId, scope?.CustomerId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(coupon.CustomerEmail) && !await MatchesEmailAsync(coupon.CustomerEmail.Trim(), session, scope, cancellationToken))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> MatchesEmailAsync(string email, CheckoutSession session, CheckoutDiscountScope scope, CancellationToken cancellationToken)
    {
        if (string.Equals(email, scope?.CustomerEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (session.TryGet<CheckoutContactInfo>(out var contact) && string.Equals(email, contact.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The buyer's account is read only when nothing the checkout holds matches, since it is a lookup.
        if (string.IsNullOrEmpty(session.OwnerId) || _serviceProvider.GetService<ICustomerContactResolver>() is not { } contactResolver)
        {
            return false;
        }

        var account = await contactResolver.ResolveAsync(CustomerOwner.ForUser(session.OwnerId), guestContact: null, cancellationToken);

        return string.Equals(email, account?.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(IEnumerable<string> values, string value)
        => !string.IsNullOrWhiteSpace(value) &&
            values.Any(candidate => string.Equals(candidate?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
}
