using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Endpoints;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.RateLimits;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Api;

/// <summary>
/// The checkout API: reading a checkout, its discount codes, the ways to pay it, paying, and completing it. The
/// modules that add steps map their own endpoints beside these (contact, shipping, gift cards, store credit).
/// </summary>
internal static class CheckoutApiEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapGet("checkout/{id}", GetAsync)
            .AllowGuests()
            .WithName("GetCheckout")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a checkout: its steps, what's saved on them, and the invoice with what's left to pay.");

        group.MapPost("checkout/{id}/codes", ApplyCodeAsync)
            .AllowGuests()
            .WithRateLimitGroup(CheckoutConstants.RateLimitGroups.Payment)
            .WithName("ApplyCheckoutCode")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Puts a discount code on the checkout.");

        group.MapDelete("checkout/{id}/codes/{code}", RemoveCodeAsync)
            .AllowGuests()
            .WithName("RemoveCheckoutCode")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes a discount code off the checkout.");

        group.MapGet("checkout/{id}/payment-methods", GetPaymentMethodsAsync)
            .AllowGuests()
            .WithName("ListCheckoutPaymentMethods")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists the ways the checkout can be paid, with what a client needs to collect each payment itself.");

        group.MapPost("checkout/{id}/payment", BeginPaymentAsync)
            .AllowGuests()
            .WithIdempotency()
            .WithRateLimitGroup(CheckoutConstants.RateLimitGroups.Payment)
            .WithName("BeginCheckoutPayment")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithSummary("Begins paying what's due with a payment method. The answer says, per charge, whether the client confirms it with the provider's SDK or sends the buyer to the provider's page.");

        group.MapPost("checkout/{id}/complete", CompleteAsync)
            .AllowGuests()
            .WithRateLimitGroup(CheckoutConstants.RateLimitGroups.Payment)
            .WithName("CompleteCheckout")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithSummary("Completes the checkout once it's paid, placing the order. While the provider hasn't answered it says pending: ask again shortly.");

        group.MapDelete("checkout/{id}", CancelAsync)
            .AllowGuests()
            .WithName("CancelCheckout")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cancels a checkout that isn't paid.");

        return routes;
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> GetAsync(string id, HttpContext httpContext, ICheckoutSessionStore store)
        => await CheckoutApi.FindAnyAsync(store, id, httpContext.RequestAborted) is { } session
            ? TypedResults.Ok(CheckoutApi.Read(session))
            : ApiProblems.NotFound();

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> ApplyCodeAsync(
        string id,
        ApiCodeRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutCouponService coupons)
    {
        if (string.IsNullOrWhiteSpace(request?.Code))
        {
            return ApiProblems.Validation("code", "Enter a code.");
        }

        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (CheckoutCouponService.RefusesDiscounts(flow))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Codes can't be used on this checkout.");
        }

        if (await coupons.ApplyAsync(flow, request.Code.Trim(), GetClientAddress(httpContext)) is { } error)
        {
            return ApiProblems.Validation("code", error);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemoveCodeAsync(
        string id,
        string code,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        CheckoutCouponService coupons)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        if (await coupons.RemoveAsync(new CheckoutFlow(session), code))
        {
            await store.SaveAsync(session, httpContext.RequestAborted);
        }

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiPaymentMethod>>, ProblemHttpResult>> GetPaymentMethodsAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        IEnumerable<ICheckoutPaymentProvider> providers,
        ISiteService siteService)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var settings = await siteService.GetSettingsAsync<CheckoutSettings>();
        var invoice = session.TryGet<CheckoutInvoice>(out var found) ? found : null;
        var obligations = invoice is null ? [] : CheckoutObligations.GetExpectedObligationIds(invoice);

        // The same methods the payment step offers: only those that can settle what this invoice owes.
        var eligible = await CheckoutPaymentMethodSelector.GetAvailableAsync(
            CheckoutPaymentMethodSelector.GetEligible(providers, obligations, session.SettlesExistingBalance, session.SavePaymentMethod),
            session,
            httpContext.RequestAborted);
        var selected = CheckoutPaymentMethodSelector.ResolveDefault(eligible, settings.DefaultPaymentMethod);
        var clients = httpContext.RequestServices.GetServices<ICheckoutPaymentClientConfigurationProvider>().ToArray();
        var methods = new List<ApiPaymentMethod>();

        foreach (var provider in eligible)
        {
            var client = Array.Find(clients, entry => string.Equals(entry.ProviderKey, provider.Key, StringComparison.OrdinalIgnoreCase));
            var isGateway = CheckoutPaymentMethodSelector.IsGateway(provider);

            methods.Add(new ApiPaymentMethod(
                provider.Key,
                provider.DisplayName,
                provider is ICheckoutPaymentProviderDescription described && !string.IsNullOrEmpty(described.Description) ? described.Description : null,
                string.Equals(provider.Key, selected, StringComparison.OrdinalIgnoreCase),
                isGateway ? "gateway" : "offline",
                client is null ? null : await client.GetAsync(session, httpContext.RequestAborted)));
        }

        return TypedResults.Ok<IReadOnlyList<ApiPaymentMethod>>(methods);
    }

    private static async Task<Results<Ok<ApiPaymentBegun>, ValidationProblem, ProblemHttpResult>> BeginPaymentAsync(
        string id,
        ApiBeginPaymentRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ICheckoutEngine engine,
        IPaymentAttemptLimiter limiter)
    {
        if (string.IsNullOrWhiteSpace(request?.PaymentMethod))
        {
            return ApiProblems.Validation("paymentMethod", "Choose how to pay.");
        }

        if (!await limiter.AcquireAsync("api-checkout-begin", GetDiscriminator(httpContext, id)))
        {
            return ApiProblems.TooManyRequests("Too many payment attempts. Wait a moment and try again.");
        }

        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is null)
        {
            return ApiProblems.NotFound();
        }

        var outcome = await engine.BeginPaymentAsync(id, new BeginPaymentOptions
        {
            ProviderKey = request.PaymentMethod,
            ReturnUrl = request.ReturnUrl,
            CancelUrl = request.CancelUrl,

            // What a client may pass a provider is bounded, and never says to keep a card or charge a kept one.
            ProviderData = PaymentEndpoints.SanitizeProviderData(request.ProviderData),
        }, httpContext.RequestAborted);

        if (!outcome.Succeeded)
        {
            return outcome.InvoiceChanged
                ? ApiProblems.Conflict(ApiProblems.Codes.InvoiceChanged, outcome.ErrorMessage)
                : ApiProblems.Validation("paymentMethod", outcome.ErrorMessage);
        }

        return TypedResults.Ok(new ApiPaymentBegun(
            outcome.RequiresAction,
            outcome.Steps.Select(step => new ApiPaymentStep(step.ObligationId, step.AttemptId, step.ClientSecret, step.RedirectUrl, step.RequiresAction)).ToList()));
    }

    private static async Task<Results<Ok<ApiCheckoutCompletion>, ProblemHttpResult>> CompleteAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ICheckoutEngine engine,
        IPaymentAttemptLimiter limiter)
    {
        // Polled while a provider settles, so it's throttled: a stuck client mustn't hammer the provider through it.
        if (!await limiter.AcquireAsync("api-checkout-complete", GetDiscriminator(httpContext, id)))
        {
            return ApiProblems.TooManyRequests("Too many status checks. Wait a moment and try again.");
        }

        if (await CheckoutApi.FindAnyAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var result = await engine.TryCompleteAsync(id, httpContext.RequestAborted);

        return result.Status == CheckoutCompletionStatus.NotFound
            ? ApiProblems.NotFound()
            : TypedResults.Ok(CheckoutApi.Read(result, session));
    }

    private static async Task<Results<Ok<ApiCheckoutCompletion>, ProblemHttpResult>> CancelAsync(
        string id,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        ICheckoutEngine engine)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var result = await engine.CancelAsync(id, "Canceled by the buyer.", httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(result, session));
    }

    private static string GetClientAddress(HttpContext httpContext)
        => httpContext.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

    private static string GetDiscriminator(HttpContext httpContext, string id)
        => $"{GetClientAddress(httpContext)}:{id}";
}

/// <summary>
/// A discount code to put on the checkout. Gift cards have their own endpoint.
/// </summary>
public sealed class ApiCodeRequest
{
    public string Code { get; set; }
}

/// <summary>
/// How to pay what's due.
/// </summary>
public sealed class ApiBeginPaymentRequest
{
    /// <summary>
    /// The payment method's key, from the checkout's payment methods.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Where a provider that takes the buyer to its own page sends them back after paying.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Where it sends them when they give up.
    /// </summary>
    public string CancelUrl { get; set; }

    /// <summary>
    /// What the provider needs from the client, such as a payment method a client SDK created.
    /// </summary>
    public Dictionary<string, string> ProviderData { get; set; }
}
