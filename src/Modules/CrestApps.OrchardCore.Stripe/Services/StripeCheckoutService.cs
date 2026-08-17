using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;
using Stripe.Checkout;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe Checkout Session operations.
/// </summary>
public sealed class StripeCheckoutService : IStripeCheckoutService
{
    private readonly StripeClient _stripeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCheckoutService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    public StripeCheckoutService(StripeClient stripeClient)
    {
        _stripeClient = stripeClient;
    }

    /// <summary>
    /// Creates a Stripe Checkout Session from the provided request.
    /// </summary>
    /// <param name="request">The checkout session creation request.</param>
    /// <returns>The created checkout session details returned by Stripe.</returns>
    public async Task<CreateCheckoutSessionResponse> CreateAsync(CreateCheckoutSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = BuildOptions(request);

        var sessionService = new SessionService(_stripeClient);
        var session = await sessionService.CreateAsync(options, request.ToRequestOptions());

        return new CreateCheckoutSessionResponse
        {
            Id = session.Id,
            Url = session.Url,
            ClientSecret = session.ClientSecret,
            Status = session.Status,
        };
    }

    /// <summary>
    /// Retrieves an existing Stripe Checkout Session by identifier.
    /// </summary>
    /// <param name="checkoutSessionId">The Stripe Checkout Session identifier.</param>
    /// <returns>The checkout session details, or <see langword="null"/> when the session cannot be found.</returns>
    public async Task<CheckoutSessionDetails> GetAsync(string checkoutSessionId)
    {
        if (string.IsNullOrEmpty(checkoutSessionId))
        {
            return null;
        }

        var sessionService = new SessionService(_stripeClient);

        Session session;

        try
        {
            session = await sessionService.GetAsync(checkoutSessionId, new SessionGetOptions
            {
                Expand = ["subscription", "customer"],
            });
        }
        catch (StripeException)
        {
            return null;
        }

        if (session == null)
        {
            return null;
        }

        return new CheckoutSessionDetails
        {
            Id = session.Id,
            Status = session.Status,
            PaymentStatus = session.PaymentStatus,
            Mode = session.Mode,
            CustomerId = session.CustomerId ?? session.Customer?.Id,
            ClientReferenceId = session.ClientReferenceId,
            SubscriptionId = session.SubscriptionId ?? session.Subscription?.Id,
            Currency = session.Currency,
            // Stripe reports amounts in the smallest currency unit; convert using the currency's precision.
            AmountTotal = session.AmountTotal.HasValue
                ? StripeCurrency.FromMinorUnits(session.AmountTotal.Value, session.Currency)
                : 0,
            Livemode = session.Livemode,
        };
    }

    /// <summary>
    /// Maps the transport-neutral request to Stripe's <see cref="SessionCreateOptions"/>. Kept as a
    /// deliberately thin, side-effect-free projection so the interesting business logic (building the
    /// request from an invoice) lives in a testable place upstream.
    /// </summary>
    /// <param name="request">The checkout session request to map.</param>
    /// <returns>The Stripe checkout session creation options.</returns>
    internal static SessionCreateOptions BuildOptions(CreateCheckoutSessionRequest request)
    {
        var options = new SessionCreateOptions
        {
            Mode = request.Mode,
            UiMode = request.UiMode,
            LineItems = request.LineItems
                .Select(item => new SessionLineItemOptions
                {
                    Price = item.PriceId,
                    Quantity = item.Quantity,
                })
                .ToList(),
        };

        if (!string.IsNullOrEmpty(request.CustomerId))
        {
            options.Customer = request.CustomerId;
        }
        else if (!string.IsNullOrEmpty(request.CustomerEmail))
        {
            options.CustomerEmail = request.CustomerEmail;
        }

        if (!string.IsNullOrEmpty(request.ClientReferenceId))
        {
            options.ClientReferenceId = request.ClientReferenceId;
        }

        if (request.Metadata is { Count: > 0 })
        {
            options.Metadata = new Dictionary<string, string>(request.Metadata);
        }

        // Only 'subscription' sessions accept subscription data.
        if (string.Equals(request.Mode, "subscription", StringComparison.OrdinalIgnoreCase) &&
            (request.SubscriptionMetadata is { Count: > 0 } || request.TrialPeriodDays.HasValue))
        {
            options.SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = request.TrialPeriodDays,
            };

            if (request.SubscriptionMetadata is { Count: > 0 })
            {
                options.SubscriptionData.Metadata = new Dictionary<string, string>(request.SubscriptionMetadata);
            }
        }

        // Hosted checkout redirects; embedded checkout returns inline.
        if (string.Equals(request.UiMode, "embedded", StringComparison.OrdinalIgnoreCase))
        {
            options.ReturnUrl = request.ReturnUrl;
        }
        else
        {
            options.SuccessUrl = request.SuccessUrl;
            options.CancelUrl = request.CancelUrl;
        }

        return options;
    }
}
