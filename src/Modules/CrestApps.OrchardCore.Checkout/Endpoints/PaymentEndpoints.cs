using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Checkout.Endpoints;

/// <summary>
/// The JSON endpoints the checkout page uses to begin a payment and to watch it settle.
/// </summary>
/// <remarks>
/// Payment cannot be a plain form post. An embedded provider needs a client secret to confirm the card in the
/// browser, and a hosted one needs a redirect, so the page has to start the payment, hand control to the
/// provider's own script, and then ask the server whether it settled. These two endpoints are that
/// conversation, and both delegate every decision to the engine.
/// </remarks>
public static class PaymentEndpoints
{
    private const int MaxProviderDataEntries = 16;
    private const int MaxProviderDataKeyLength = 64;
    private const int MaxProviderDataValueLength = 512;

    /// <summary>
    /// Adds the checkout payment endpoints.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    public static IEndpointRouteBuilder AddCheckoutPaymentEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("checkout/{sessionId}/payment/begin", BeginAsync)
            .AllowAnonymous()
            .WithName(CheckoutConstants.RouteNames.BeginPayment)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(CheckoutConstants.RateLimitGroups.Payment));

        builder.MapPost("checkout/{sessionId}/payment/status", StatusAsync)
            .AllowAnonymous()
            .WithName(CheckoutConstants.RouteNames.PaymentStatus)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(CheckoutConstants.RateLimitGroups.Payment));

        return builder;
    }

    private static async Task<IResult> BeginAsync(
        string sessionId,
        [FromBody] BeginPaymentRequest model,
        HttpContext httpContext,
        ICheckoutSessionStore sessionStore,
        ICheckoutEngine engine,
        IPaymentAttemptLimiter limiter,
        CancellationToken cancellationToken)
    {
        if (!IsSameOrigin(httpContext))
        {
            // These endpoints are anonymous and antiforgery-exempt because they carry JSON rather than a form.
            // Requiring a same-origin request is what keeps another site from driving a visitor's checkout.
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(model?.ProviderKey))
        {
            return Error("The payment request was incomplete.", StatusCodes.Status400BadRequest);
        }

        if (!await limiter.AcquireAsync("checkout-begin", BuildDiscriminator(httpContext, sessionId)))
        {
            return Error("Too many payment attempts. Please wait a moment and try again.", StatusCodes.Status429TooManyRequests);
        }

        // Resolving through the ownership-checked lookup keeps one visitor from starting a payment on
        // another's checkout by guessing a session id.
        var session = await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Pending, cancellationToken);

        session ??= await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.AwaitingProvider, cancellationToken);
        session ??= await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.PaymentPending, cancellationToken);

        if (session is null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await engine.BeginPaymentAsync(
            sessionId,
            new BeginPaymentOptions
            {
                ProviderKey = model.ProviderKey,
                ReturnUrl = model.ReturnUrl,
                CancelUrl = model.CancelUrl,
                ProviderData = SanitizeProviderData(model.ProviderData),
            },
            cancellationToken);

        if (!outcome.Succeeded)
        {
            return Error(outcome.ErrorMessage, StatusCodes.Status400BadRequest);
        }

        return TypedResults.Ok(new
        {
            requiresAction = outcome.RequiresAction,
            steps = outcome.Steps.Select(step => new
            {
                obligationId = step.ObligationId,
                attemptId = step.AttemptId,
                clientSecret = step.ClientSecret,
                redirectUrl = step.RedirectUrl,
                requiresAction = step.RequiresAction,
            }),
        });
    }

    private static async Task<IResult> StatusAsync(
        string sessionId,
        HttpContext httpContext,
        ICheckoutEngine engine,
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptLimiter limiter,
        CancellationToken cancellationToken)
    {
        if (!IsSameOrigin(httpContext))
        {
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return Error("The payment request was incomplete.", StatusCodes.Status400BadRequest);
        }

        // Polling is throttled too. Without a limit a stuck page could hammer the provider's API through the
        // verification path for as long as the tab stayed open.
        if (!await limiter.AcquireAsync("checkout-status", BuildDiscriminator(httpContext, sessionId)))
        {
            return Error("Too many status checks. Please wait a moment and try again.", StatusCodes.Status429TooManyRequests);
        }

        // The store only returns a live checkout that belongs to this caller. Completing is idempotent, but
        // the answer names what the checkout is still waiting on, and that is not anybody else's to read.
        var owned = await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Pending, cancellationToken)
            ?? await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.AwaitingProvider, cancellationToken)
            ?? await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.PaymentPending, cancellationToken)
            ?? await sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Completed, cancellationToken);

        if (owned is null)
        {
            return TypedResults.NotFound();
        }

        var result = await engine.TryCompleteAsync(sessionId, cancellationToken);

        if (result.Status == CheckoutCompletionStatus.NotFound)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new
        {
            status = result.Status.ToString(),
            completed = result.IsCompleted,
            blockingStepKey = result.BlockingStepKey,
            errorMessage = result.ErrorMessage,
        });
    }

    // The provider data comes straight from the browser, so it is bounded before it reaches a provider or a
    // durable record. An unbounded dictionary posted by anyone would otherwise be a cheap way to push
    // arbitrary volumes of attacker-controlled text through the payment path.
    private static Dictionary<string, string> SanitizeProviderData(Dictionary<string, string> providerData)
    {
        if (providerData is null || providerData.Count == 0)
        {
            return null;
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in providerData)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Key.Length > MaxProviderDataKeyLength)
            {
                continue;
            }

            if (pair.Value is not null && pair.Value.Length > MaxProviderDataValueLength)
            {
                continue;
            }

            sanitized[pair.Key] = pair.Value;

            if (sanitized.Count == MaxProviderDataEntries)
            {
                break;
            }
        }

        return sanitized.Count == 0 ? null : sanitized;
    }

    // Combines the caller and the checkout so abuse is throttled per visitor per checkout, rather than one
    // busy customer throttling everyone.
    private static string BuildDiscriminator(HttpContext httpContext, string sessionId)
        => $"{httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"}:{sessionId}";

    // A cross-site page can send a JSON POST without a preflight only in narrow cases, so this is defense in
    // depth rather than the only guard: the ownership check on the session is the primary one.
    private static bool IsSameOrigin(HttpContext httpContext)
    {
        var request = httpContext?.Request;

        if (request is null)
        {
            return false;
        }

        // Browsers that send fetch metadata tell us directly where the request came from.
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSite) && fetchSite.Count > 0)
        {
            return fetchSite[0] is "same-origin" or "same-site" or "none";
        }

        if (!request.Headers.TryGetValue("Origin", out var origin) || origin.Count == 0)
        {
            // No Origin header at all means a non-browser client, which cannot be a cross-site attack against
            // a visitor's session.
            return true;
        }

        return Uri.TryCreate(origin[0], UriKind.Absolute, out var originUri) &&
            string.Equals(originUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }

    // The checkout scripts always read the body as JSON, so an error must be JSON too: an empty body would
    // throw during parsing and leave the customer looking at a stalled button with no explanation.
    private static JsonHttpResult<CheckoutErrorResponse> Error(string message, int statusCode)
        => TypedResults.Json(
            new CheckoutErrorResponse(message, message),
            statusCode: statusCode);

    // Both shapes are emitted because the checkout script reads 'errorMessage' while other clients (and the
    // subscription scripts this replaces) read 'error'.
    private sealed record CheckoutErrorResponse(string Error, string ErrorMessage);

    /// <summary>
    /// The body of a begin-payment request.
    /// </summary>
    public sealed class BeginPaymentRequest
    {
        /// <summary>
        /// Gets or sets the key of the provider the customer chose.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// Gets or sets the URL a hosted provider returns to on success.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL a hosted provider returns to on cancellation.
        /// </summary>
        public string CancelUrl { get; set; }

        /// <summary>
        /// Gets or sets the values the chosen provider's own client script produced before payment began, for
        /// example a tokenized payment method. Only the provider that asked for a value interprets it.
        /// </summary>
        public Dictionary<string, string> ProviderData { get; set; }
    }
}
