using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe PaymentIntent operations.
/// </summary>
public sealed class StripePaymentIntentService : IStripePaymentIntentService
{
    /// <summary>
    /// The Stripe API version shared payment tokens need while they are in preview.
    /// </summary>
    public const string SharedPaymentTokenApiVersion = "2026-09-30.preview";

    private readonly StripeClient _stripeClient;
    private readonly PaymentIntentService _paymentIntentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentIntentService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    public StripePaymentIntentService(StripeClient stripeClient)
    {
        _stripeClient = stripeClient;
        _paymentIntentService = new PaymentIntentService(stripeClient);
    }

    /// <summary>
    /// Confirms an existing Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The payment intent confirmation request.</param>
    /// <returns>The confirmed payment intent details returned by Stripe.</returns>
    public async Task<ConfirmPaymentIntentResponse> ConfirmAsync(ConfirmPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        var confirmOptions = new PaymentIntentConfirmOptions();

        if (!string.IsNullOrEmpty(model.PaymentMethodId))
        {
            confirmOptions.PaymentMethod = model.PaymentMethodId;
        }

        PaymentIntent result;

        try
        {
            result = await _paymentIntentService.ConfirmAsync(model.PaymentIntentId, confirmOptions, model.ToRequestOptions());
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "payment_intent_unexpected_state")
        {
            // Confirmation must be idempotent. By the time this runs, the PaymentIntent may already
            // have reached a terminal state through client-side confirmation (Payment Elements),
            // Stripe's automatic subscription collection, or an earlier at-least-once webhook delivery.
            // Stripe rejects re-confirming such an intent with 'payment_intent_unexpected_state'. Treat
            // that as success by returning the authoritative current state instead of throwing, which
            // would fail the webhook and make Stripe retry a permanently failing request forever.
            result = await _paymentIntentService.GetAsync(model.PaymentIntentId, options: null, model.ToRequestOptions());
        }

        return new ConfirmPaymentIntentResponse
        {
            Status = result.Status,
            Id = result.Id,
            Amount = result.Amount,
            Currency = result.Currency,
            CustomerId = result.Customer?.Id,
        };
    }

    /// <summary>
    /// Creates and confirms a Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The payment intent creation request.</param>
    /// <returns>The created payment intent details returned by Stripe.</returns>
    public async Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = StripeCurrency.ToMinorUnits(model.Amount ?? 0, model.Currency), // Currency-aware minor units (e.g. 1000 == $10.00, 500 == ¥500).
            Currency = model.Currency,
            PaymentMethod = model.PaymentMethodId,
            Customer = model.CustomerId,
            Confirm = true,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = model.Metadata,
        };

        var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, model.ToRequestOptions());

        return new CreatePaymentIntentResponse()
        {
            Id = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }

    /// <summary>
    /// Creates an unconfirmed Stripe PaymentIntent for a generic checkout payment collected through
    /// embedded Stripe Elements.
    /// </summary>
    /// <param name="model">The checkout payment intent creation request.</param>
    /// <returns>The created payment intent details, including the client secret used to confirm it.</returns>
    public async Task<CreatePaymentIntentResponse> CreateForCheckoutAsync(CreateCheckoutPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            // Currency-aware minor units (for example 1000 == $10.00, 500 == ¥500).
            Amount = StripeCurrency.ToMinorUnits(model.Amount, model.Currency),
            Currency = model.Currency,
            Customer = model.CustomerId,
            Description = model.Description,

            // The customer confirms with Stripe.js using the returned client secret, so the payment goes
            // through Strong Customer Authentication instead of being confirmed off-session.
            Confirm = false,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Metadata = model.Metadata,
        };

        if (!string.IsNullOrEmpty(model.PaymentMethodId))
        {
            paymentIntentOptions.PaymentMethod = model.PaymentMethodId;
        }

        if (model.SaveForOffSessionUse)
        {
            // Authenticating this payment with the customer present is what lets the card be charged later
            // without them: the bank is told up front that future charges will follow.
            paymentIntentOptions.SetupFutureUsage = "off_session";
        }
        else if (model.SaveForOnSessionUse)
        {
            // Kept for the customer's own later purchases, which they confirm themselves.
            paymentIntentOptions.SetupFutureUsage = "on_session";
        }

        var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, model.ToRequestOptions());

        return new CreatePaymentIntentResponse
        {
            Id = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }

    /// <summary>
    /// Charges a saved payment method server-side, without the customer present.
    /// </summary>
    /// <param name="model">The charge to make.</param>
    /// <returns>Whether Stripe accepted the charge, and why not when it did not.</returns>
    public async Task<ChargeDelegatedCredentialResponse> ChargeDelegatedAsync(ChargeDelegatedCredentialRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.Credential);

        if (string.Equals(model.CredentialType, DelegatedCredentialTypes.StripeSharedPaymentToken, StringComparison.Ordinal))
        {
            return await ChargeSharedPaymentTokenAsync(model);
        }

        if (!string.Equals(model.CredentialType, DelegatedCredentialTypes.GooglePayToken, StringComparison.Ordinal))
        {
            return new ChargeDelegatedCredentialResponse
            {
                InvalidCredential = true,
                ErrorMessage = "Stripe doesn't take this kind of credential.",
            };
        }

        if (GetGooglePayCardToken(model.Credential) is not { } cardToken)
        {
            return new ChargeDelegatedCredentialResponse
            {
                InvalidCredential = true,
                ErrorMessage = "The Google Pay token isn't one Stripe issued.",
            };
        }

        var options = new PaymentIntentCreateOptions
        {
            Amount = StripeCurrency.ToMinorUnits(model.Amount, model.Currency),
            Currency = model.Currency,
            Description = model.Description,
            Metadata = model.Metadata,

            // Confirmed here: the agent already has the buyer's approval, and there is no browser to confirm it in.
            Confirm = true,
        };

        // No payment_method_types: current API versions refuse it, and the card token says what pays.
        options.AddExtraParam("payment_method_data[type]", "card");
        options.AddExtraParam("payment_method_data[card][token]", cardToken);

        foreach (var (name, value) in GetThreeDSecureFields(model.ThreeDSecure))
        {
            options.AddExtraParam(name, value);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl))
        {
            options.ReturnUrl = model.ReturnUrl;
        }

        try
        {
            return await WithCardBrandAsync(ToResponse(await _paymentIntentService.CreateAsync(options, model.ToRequestOptions())));
        }
        catch (StripeException ex) when (ex.StripeError is not null)
        {
            return ToResponse(ex.StripeError, ex.Message);
        }
    }

    // Shared payment tokens are a preview of Stripe's API, which the typed client doesn't send its version for, so the
    // request is made raw with the preview version.
    private async Task<ChargeDelegatedCredentialResponse> ChargeSharedPaymentTokenAsync(ChargeDelegatedCredentialRequest model)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("amount", StripeCurrency.ToMinorUnits(model.Amount, model.Currency).ToString(CultureInfo.InvariantCulture)),
            new("currency", model.Currency?.ToLowerInvariant()),
            new("confirm", "true"),

            // The preview version refuses payment_method_types: the token says what pays.
            new("payment_method_data[shared_payment_granted_token]", model.Credential),
        };

        if (!string.IsNullOrEmpty(model.Description))
        {
            fields.Add(new("description", model.Description));
        }

        // No return_url: for a token-backed PaymentIntent the agent gives it, and Stripe refuses one from the seller.
        foreach (var (key, value) in model.Metadata ?? [])
        {
            fields.Add(new($"metadata[{key}]", value));
        }

        fields.AddRange(GetThreeDSecureFields(model.ThreeDSecure));

        return await PostAsync("/v1/payment_intents", fields, model.IdempotencyKey, preview: true, paymentIntentId: null);
    }

    /// <summary>
    /// Confirms again a PaymentIntent made from a delegated credential, with the result of a 3-D Secure authentication
    /// run outside Stripe.
    /// </summary>
    /// <param name="model">The PaymentIntent and the 3-D Secure result.</param>
    /// <returns>Whether Stripe took the payment, and why not when it did not.</returns>
    public Task<ChargeDelegatedCredentialResponse> ConfirmDelegatedAsync(ConfirmDelegatedCredentialRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);
        ArgumentNullException.ThrowIfNull(model.ThreeDSecure);

        // A PaymentIntent made from a shared payment token is only reachable with the preview version it was made with.
        return PostAsync(
            $"/v1/payment_intents/{Uri.EscapeDataString(model.PaymentIntentId)}/confirm",
            GetThreeDSecureFields(model.ThreeDSecure),
            model.IdempotencyKey,
            preview: string.Equals(model.CredentialType, DelegatedCredentialTypes.StripeSharedPaymentToken, StringComparison.Ordinal),
            paymentIntentId: model.PaymentIntentId);
    }

    /// <summary>
    /// Returns the form fields that import a 3-D Secure result run outside Stripe
    /// (<c>payment_method_options[card][three_d_secure]</c>), with <c>error_on_requires_action</c> so Stripe fails the
    /// payment instead of asking the buyer to authenticate again. Empty when there is no result.
    /// </summary>
    /// <param name="threeDSecure">The 3-D Secure result, or <see langword="null"/>.</param>
    internal static List<KeyValuePair<string, string>> GetThreeDSecureFields(StripeThreeDSecureImport threeDSecure)
    {
        if (threeDSecure is null)
        {
            return [];
        }

        const string prefix = "payment_method_options[card][three_d_secure]";

        var fields = new List<KeyValuePair<string, string>>
        {
            new($"{prefix}[cryptogram]", threeDSecure.Cryptogram),
            new($"{prefix}[electronic_commerce_indicator]", threeDSecure.ElectronicCommerceIndicator),
            new($"{prefix}[transaction_id]", threeDSecure.TransactionId),
            new($"{prefix}[version]", threeDSecure.Version),
        };

        if (!string.IsNullOrEmpty(threeDSecure.AresTransStatus))
        {
            fields.Add(new($"{prefix}[ares_trans_status]", threeDSecure.AresTransStatus));
        }

        // The buyer already authenticated; Stripe must not start a challenge of its own on a soft decline.
        fields.Add(new("error_on_requires_action", "true"));

        return fields;
    }

    // Posts a form to the PaymentIntents API, raw, so a shared payment token's preview version can be sent.
    private async Task<ChargeDelegatedCredentialResponse> PostAsync(string path, List<KeyValuePair<string, string>> fields, string idempotencyKey, bool preview, string paymentIntentId)
    {
        using var content = new FormUrlEncodedContent(fields);
        var body = await content.ReadAsStringAsync();
        var options = new RawRequestOptions
        {
            IdempotencyKey = idempotencyKey,
        };

        if (preview)
        {
            options.AdditionalHeaders = new Dictionary<string, string>
            {
                ["Stripe-Version"] = SharedPaymentTokenApiVersion,
            };
        }

        StripeResponse response;

        try
        {
            response = await _stripeClient.RawRequestAsync(HttpMethod.Post, path, body, options);
        }
        catch (StripeException ex) when (ex.StripeError is not null)
        {
            return await FromErrorAsync(ex.StripeError, ex.Message, paymentIntentId);
        }

        if ((int)response.StatusCode >= 400)
        {
            var error = TryReadError(response.Content);

            return error is null
                ? new ChargeDelegatedCredentialResponse { ErrorMessage = $"Stripe answered {(int)response.StatusCode}." }
                : await FromErrorAsync(error, error.Message, paymentIntentId);
        }

        return await WithCardBrandAsync(ToResponse(StripeEntity.FromJson<PaymentIntent>(response.Content)));
    }

    // A confirm Stripe refuses because the PaymentIntent already moved on, such as one an earlier try confirmed, answers
    // with where it stands now rather than a failure, so a retry never reports a payment that went through as refused.
    private async Task<ChargeDelegatedCredentialResponse> FromErrorAsync(StripeError error, string fallbackMessage, string paymentIntentId)
    {
        if (paymentIntentId is not null && string.Equals(error.Code, "payment_intent_unexpected_state", StringComparison.Ordinal))
        {
            try
            {
                var current = await _paymentIntentService.GetAsync(paymentIntentId);

                if (current.Status is "succeeded" or "processing")
                {
                    return ToResponse(current);
                }
            }
            catch (StripeException)
            {
                // What Stripe refused is answer enough; the checkout verifies the payment before it counts anyway.
            }
        }

        return ToResponse(error, fallbackMessage);
    }

    // The bank asked the buyer to approve the payment: the card's brand tells an agent that authenticates the buyer itself
    // which directory server to use. Stripe answers without it unless the payment method is expanded.
    private async Task<ChargeDelegatedCredentialResponse> WithCardBrandAsync(ChargeDelegatedCredentialResponse response)
    {
        if (!response.RequiresAction || !string.IsNullOrEmpty(response.CardBrand) || string.IsNullOrEmpty(response.PaymentIntentId))
        {
            return response;
        }

        try
        {
            var paymentIntent = await _paymentIntentService.GetAsync(response.PaymentIntentId, new PaymentIntentGetOptions
            {
                Expand = ["payment_method"],
            });

            response.CardBrand = paymentIntent.PaymentMethod?.Card?.Brand;
        }
        catch (StripeException)
        {
            // The brand is a convenience; the payment stands without it.
        }

        return response;
    }

    private static StripeError TryReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty("error", out var error)
                ? StripeEntity.FromJson<StripeError>(error.GetRawText())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChargeDelegatedCredentialResponse ToResponse(PaymentIntent paymentIntent)
        => new()
        {
            Succeeded = paymentIntent.Status is "succeeded" or "processing",
            RequiresAction = paymentIntent.Status == "requires_action",
            RedirectUrl = paymentIntent.NextAction?.RedirectToUrl?.Url,
            PaymentIntentId = paymentIntent.Id,
            Status = paymentIntent.Status,
            ErrorMessage = paymentIntent.LastPaymentError?.Message,
            DeclineCode = paymentIntent.LastPaymentError?.DeclineCode,
            Declined = paymentIntent.Status is "requires_payment_method" or "canceled",
            CardBrand = paymentIntent.PaymentMethod?.Card?.Brand,
        };

    private static ChargeDelegatedCredentialResponse ToResponse(StripeError error, string fallbackMessage)
        => new()
        {
            PaymentIntentId = error.PaymentIntent?.Id,
            Status = error.PaymentIntent?.Status,
            ErrorMessage = error.Message ?? fallbackMessage,
            DeclineCode = error.DeclineCode,

            // A used, expired or revoked token, or one issued to another account, is refused before any card is asked.
            InvalidCredential = error.Type == "invalid_request_error",

            Declined = IsFinal(error),
            // Stripe was asked not to start its own authentication, and the bank wants the buyer to approve the payment.
            RequiresAuthentication = string.Equals(error.Code, "authentication_required", StringComparison.Ordinal),
            CardBrand = error.PaymentMethod?.Card?.Brand,
        };

    // Whether Stripe's error is its final answer, with no money taken: a card error (the bank refused), or a request it
    // refused outright (a used token, a bad parameter), which Stripe answers before it charges anything. A new try is
    // then a new charge and needs a new idempotency key, or Stripe keeps replaying the refusal. Everything else (a
    // server error, a rate limit, an idempotency conflict) leaves the outcome unknown, and only a retry with the same
    // key is safe: Stripe replays what it did instead of charging again.
    internal static bool IsFinal(StripeError error)
        => error?.Type is "card_error" or "invalid_request_error";

    // A Google Pay token whose gateway is Stripe is a JSON object holding the card token Stripe made for it.
    internal static string GetGooglePayCardToken(string credential)
    {
        var value = credential?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.StartsWith("tok_", StringComparison.Ordinal))
        {
            return value;
        }

        if (!value.StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);

            return document.RootElement.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String &&
                id.GetString() is { } token &&
                token.StartsWith("tok_", StringComparison.Ordinal)
                ? token
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<ChargeOffSessionResponse> ChargeOffSessionAsync(ChargeOffSessionRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.CustomerId);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentMethodId);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = StripeCurrency.ToMinorUnits(model.Amount, model.Currency),
            Currency = model.Currency,
            Customer = model.CustomerId,
            PaymentMethod = model.PaymentMethodId,
            Description = model.Description,
            Metadata = model.Metadata,

            // Confirmed here, with no customer to authenticate. Stripe uses the authorization the customer gave
            // when the card was saved; if the bank insists on authenticating anyway, the charge fails with
            // 'authentication_required' and the customer has to pay this one themselves.
            Confirm = true,
            OffSession = true,
        };

        try
        {
            var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, model.ToRequestOptions());

            return new ChargeOffSessionResponse
            {
                Succeeded = paymentIntent.Status is "succeeded" or "processing",
                PaymentIntentId = paymentIntent.Id,
                Status = paymentIntent.Status,
                ErrorMessage = paymentIntent.LastPaymentError?.Message,
                DeclineCode = paymentIntent.LastPaymentError?.DeclineCode,
                Declined = paymentIntent.Status is "requires_payment_method" or "canceled",
            };
        }
        catch (StripeException ex) when (ex.StripeError is not null)
        {
            // A declined card is an answer, not a fault: report it so the caller can record why and retry later.
            // Only a card error is that answer; a server error or a rate limit says nothing about the money.
            return new ChargeOffSessionResponse
            {
                Succeeded = false,
                PaymentIntentId = ex.StripeError.PaymentIntent?.Id,
                Status = ex.StripeError.PaymentIntent?.Status,
                ErrorMessage = ex.StripeError.Message ?? ex.Message,
                DeclineCode = ex.StripeError.DeclineCode,
                RequiresAuthentication = string.Equals(ex.StripeError.Code, "authentication_required", StringComparison.Ordinal),
                Declined = IsFinal(ex.StripeError),
            };
        }
    }

    /// <summary>
    /// Retrieves the authoritative state of a Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The retrieve request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details.</returns>
    public async Task<PaymentIntentDetails> RetrieveAsync(RetrievePaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        // The latest charge carries Radar's fraud rating, which the store sees on the order.
        var paymentIntent = await _paymentIntentService.GetAsync(model.PaymentIntentId, new PaymentIntentGetOptions
        {
            Expand = ["latest_charge"],
        });

        return ToDetails(paymentIntent);
    }

    /// <summary>
    /// Cancels a Stripe PaymentIntent that has not yet been captured.
    /// </summary>
    /// <param name="model">The cancel request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details after cancellation.</returns>
    public async Task<PaymentIntentDetails> CancelAsync(CancelPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        var cancelOptions = new PaymentIntentCancelOptions();

        if (!string.IsNullOrEmpty(model.CancellationReason))
        {
            cancelOptions.CancellationReason = model.CancellationReason;
        }

        var paymentIntent = await _paymentIntentService.CancelAsync(model.PaymentIntentId, cancelOptions, model.ToRequestOptions());

        return ToDetails(paymentIntent);
    }

    private static PaymentIntentDetails ToDetails(PaymentIntent paymentIntent)
        => new()
        {
            Id = paymentIntent.Id,
            Status = paymentIntent.Status,
            Amount = paymentIntent.Amount,
            AmountReceived = paymentIntent.AmountReceived,
            Currency = paymentIntent.Currency,
            LiveMode = paymentIntent.Livemode,
            LatestChargeId = paymentIntent.LatestChargeId,
            RiskLevel = paymentIntent.LatestCharge?.Outcome?.RiskLevel is { Length: > 0 } level && level != "not_assessed" && level != "unknown" ? level : null,
            RiskScore = paymentIntent.LatestCharge?.Outcome?.RiskScore is > 0 and var score ? (int)score : null,
            CustomerId = paymentIntent.CustomerId,
            PaymentMethodId = paymentIntent.PaymentMethodId,
            SetupFutureUsage = paymentIntent.SetupFutureUsage,
            LastPaymentErrorMessage = paymentIntent.LastPaymentError?.Message,
        };
}
