using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Builds the transport-neutral <see cref="CreateCheckoutSessionRequest"/> from a subscription
/// flow invoice. The logic is intentionally free of any Stripe SDK or HTTP dependency so it can be
/// unit tested in isolation.
/// </summary>
public static class StripeCheckoutRequestFactory
{
    /// <summary>
    /// The metadata key used to correlate a Stripe subscription (and its invoices) back to the local
    /// subscription flow session. It must match the key the Stripe webhook reads.
    /// </summary>
    public const string SessionMetadataKey = "sessionId";

    /// <summary>
    /// Determines whether an invoice can be paid through a single hosted Stripe Checkout Session.
    /// A single session maps to a single Stripe subscription, so it can only represent one billing
    /// interval and cannot separately collect an up-front one-time fee.
    /// </summary>
    public static bool IsEligible(Invoice invoice, out string reason)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var groups = invoice.GetSubscriptionGroups();

        if (groups.Count == 0)
        {
            reason = "The invoice does not contain any subscription line items.";

            return false;
        }

        if (groups.Count > 1)
        {
            reason = "Hosted Stripe Checkout supports a single billing interval per checkout. Use the Payment Elements integration for products that mix billing intervals.";

            return false;
        }

        if (invoice.InitialPaymentAmount.HasValue && invoice.InitialPaymentAmount.Value > 0)
        {
            reason = "Hosted Stripe Checkout cannot collect a separate up-front fee. Use the Payment Elements integration for products with an initial one-time charge.";

            return false;
        }

        reason = null;

        return true;
    }

    /// <summary>
    /// Creates the request. <paramref name="lineItems"/> must contain the already-resolved Stripe
    /// price identifiers and quantities that make up the subscription.
    /// </summary>
    public static CreateCheckoutSessionRequest Create(
        string sessionId,
        IEnumerable<CreateCheckoutLineItem> lineItems,
        string successUrl,
        string cancelUrl,
        string customerId = null,
        string customerEmail = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(lineItems);
        ArgumentException.ThrowIfNullOrEmpty(successUrl);
        ArgumentException.ThrowIfNullOrEmpty(cancelUrl);

        var items = lineItems
            .Where(item => !string.IsNullOrEmpty(item.PriceId) && item.Quantity > 0)
            .ToList();

        if (items.Count == 0)
        {
            throw new ArgumentException("At least one line item with a price is required.", nameof(lineItems));
        }

        return new CreateCheckoutSessionRequest
        {
            Mode = "subscription",
            UiMode = "hosted",
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            ClientReferenceId = sessionId,
            LineItems = items,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                [SessionMetadataKey] = sessionId,
            },
            SubscriptionMetadata = new Dictionary<string, string>
            {
                [SessionMetadataKey] = sessionId,
            },
        };
    }
}
