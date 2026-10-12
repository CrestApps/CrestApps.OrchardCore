using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Resolves the one Stripe customer a checkout belongs to.
/// </summary>
/// <remarks>
/// A checkout can have both a one-time obligation (a setup fee) and a recurring one (the plan), and the
/// browser tokenizes a single payment method for both. Stripe attaches that payment method to a customer
/// when the agreement is created, and then refuses to confirm any payment intent that does not name the same
/// customer. Both obligations therefore have to agree on one customer, whichever of them is begun first.
///
/// The customer is created with a key derived from the checkout session rather than from the attempt, so a
/// retried begin — or the second obligation of the same checkout — resolves the customer that already
/// exists instead of leaving a duplicate behind for every attempt.
/// </remarks>
public interface IStripeCheckoutCustomerResolver
{
    /// <summary>
    /// Returns the Stripe customer this checkout should use, creating one when it has none yet.
    /// </summary>
    /// <param name="session">The checkout session, used for the buyer's contact details.</param>
    /// <param name="sessionId">The checkout session identifier the customer is keyed by.</param>
    /// <param name="paymentMethodId">The payment method the customer is created against.</param>
    /// <param name="suppliedCustomerId">A customer the client already named, if any.</param>
    /// <returns>The Stripe customer identifier, or <see langword="null"/> when one could not be created.</returns>
    Task<string> ResolveAsync(CheckoutSession session, string sessionId, string paymentMethodId, string suppliedCustomerId = null);
}

/// <inheritdoc cref="IStripeCheckoutCustomerResolver"/>
public sealed class StripeCheckoutCustomerResolver : IStripeCheckoutCustomerResolver
{
    private readonly IStripeCustomerService _customerService;
    private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutCustomerResolver"/> class.
    /// </summary>
    /// <param name="customerService">The Stripe customer service.</param>
    public StripeCheckoutCustomerResolver(IStripeCustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <inheritdoc/>
    public async Task<string> ResolveAsync(
        CheckoutSession session,
        string sessionId,
        string paymentMethodId,
        string suppliedCustomerId = null)
    {
        if (!string.IsNullOrEmpty(suppliedCustomerId))
        {
            return suppliedCustomerId;
        }

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(paymentMethodId))
        {
            return null;
        }

        if (_resolved.TryGetValue(sessionId, out var cached))
        {
            return cached;
        }

        CheckoutContactInfo contact = null;

        session?.TryGet(out contact);

        var customer = await _customerService.CreateAsync(new CreateCustomerRequest
        {
            Name = contact?.DisplayName,
            Email = contact?.Email,
            PaymentMethodId = paymentMethodId,

            // Keyed by the checkout, not by the attempt: the two obligations of one checkout have different
            // attempts, and keying by attempt would create a second customer the prepared payment method
            // does not belong to — which is the very mismatch this type exists to prevent.
            IdempotencyKey = "checkout_" + sessionId + "_customer",
            Metadata = new Dictionary<string, string>
            {
                ["checkout_session_id"] = sessionId,
            },
        });

        var customerId = customer?.CustomerId;

        if (!string.IsNullOrEmpty(customerId))
        {
            _resolved[sessionId] = customerId;
        }

        return customerId;
    }
}
