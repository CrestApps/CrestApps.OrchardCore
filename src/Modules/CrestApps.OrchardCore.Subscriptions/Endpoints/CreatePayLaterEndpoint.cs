using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Registers the endpoint that records offline Pay Later subscription commitments.
/// </summary>
public static class CreatePayLaterEndpoint
{
    /// <summary>
    /// Adds the endpoint that processes a Pay Later subscription request.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to register the route.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddCreatePayLaterEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/pay-later/process", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreatePayLaterEndpoint)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(SubscriptionConstants.RateLimitGroups.Payment));

        return builder;
    }

    /// <summary>
    /// Handles a Pay Later request by creating subscription and payment metadata for the pending session.
    /// </summary>
    /// <param name="model">The Pay Later request.</param>
    /// <param name="clock">The clock used to timestamp the subscription commitment.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="subscriptionPaymentSession">The payment session used to store initial payment metadata.</param>
    /// <param name="paymentAttemptLimiter">The payment attempt limiter used to throttle repeated requests.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current request.</param>
    /// <param name="hostEnvironment">The host environment used to determine the gateway mode.</param>
    /// <param name="loggerFactory">The logger factory used to create endpoint logs.</param>
    /// <returns>An HTTP result that reports completion or a structured error response.</returns>
    private static async Task<IResult> HandleAsync(
        [FromBody] PayLaterRequest model,
        IClock clock,
        ISubscriptionSessionStore subscriptionSessionStore,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IPaymentAttemptLimiter paymentAttemptLimiter,
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment hostEnvironment,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(CreatePayLaterEndpoint).FullName);

        if (string.IsNullOrEmpty(model?.SessionId))
        {
            return Error("Invalid request data.", errorCode: 1, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContextAccessor.HttpContext, "pay-later", model.SessionId))
        {
            return PaymentEndpointThrottle.TooManyRequests();
        }

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            logger.LogWarning("Pay Later was requested for session '{SessionId}' but no matching pending session was found for the current user.", model.SessionId);

            return Error("The subscription session could not be found or has expired. Please start the sign up again.", errorCode: 2, statusCode: StatusCodes.Status404NotFound);
        }

        if (!session.TryGet<Invoice>(out var invoice))
        {
            logger.LogError("Pay Later was requested for session '{SessionId}' but the session has no invoice attached.", model.SessionId);

            return Error("The subscription is missing billing information. Please start the sign up again.", errorCode: 3, statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            await ProcessAsync(model.SessionId, session, invoice, clock, subscriptionPaymentSession, subscriptionSessionStore, hostEnvironment);
        }
        catch (Exception ex)
        {
            // Surface a meaningful, non-technical reason to the checkout script instead of a bare 500,
            // which the UI can only render as a generic "Unexpected error".
            logger.LogError(ex, "An error occurred while recording the Pay Later commitment for session '{SessionId}'.", model.SessionId);

            return Error("We could not record your Pay Later commitment. This subscription plan may be misconfigured. Please contact the site administrator.", errorCode: 4, statusCode: StatusCodes.Status500InternalServerError);
        }

        return TypedResults.Ok(new
        {
            status = "completed",
        });
    }

    internal static async Task ProcessAsync(
        string sessionId,
        SubscriptionSession session,
        Invoice invoice,
        IClock clock,
        SubscriptionPaymentSession subscriptionPaymentSession,
        ISubscriptionSessionStore subscriptionSessionStore,
        IHostEnvironment hostEnvironment)
    {
        // Reflect the deployment environment instead of always reporting 'Live', which would mislabel
        // test transactions. Pay Later has no external gateway, so a non-production deployment records
        // its offline commitments as test data.
        var gatewayMode = hostEnvironment.IsProduction() ? GatewayMode.Live : GatewayMode.Testing;

        var now = clock.UtcNow;

        var collection = new SubscriptionsMetadata()
        {
            Subscriptions = [],
        };

        // Here we have to group subscriptions per duration to determine the proper expiration date.
        collection.Subscriptions.Add(new SubscriptionInfo
        {
            Gateway = SubscriptionConstants.PayLaterProcessorKey,
            GatewayMode = gatewayMode,
            StartedAt = now,
            ExpiresAt = null,
        });

        session.Put(collection);

        await subscriptionPaymentSession.SetAsync(sessionId, new InitialPaymentMetadata()
        {
            TransactionId = IdGenerator.GenerateId(),
            Amount = invoice.InitialPaymentAmount ?? 0,
            Currency = invoice.Currency,
            GatewayMode = gatewayMode,
            GatewayId = SubscriptionConstants.PayLaterProcessorKey,
        });

        var metadata = new SubscriptionPaymentsMetadata()
        {
            Payments = new Dictionary<string, PaymentInfo>(),
        };

        // Group line items by subscription duration, ensuring that each subscription has a single, unified expiration date.
        var subscriptionGroups = invoice.GetSubscriptionGroups();

        var subscriptionPaymentMetadata = new SubscriptionsMetadata()
        {
            Subscriptions = [],
        };

        foreach (var subscription in subscriptionGroups)
        {
            var transactionId = IdGenerator.GenerateId();
            var subscriptionId = IdGenerator.GenerateId();

            metadata.Payments[transactionId] = new PaymentInfo()
            {
                TransactionId = transactionId,
                SubscriptionId = subscriptionId,
                Currency = invoice.Currency,
                Amount = subscription.Value.Sum(x => x.GetLineTotal()),
                GatewayMode = gatewayMode,
                GatewayId = SubscriptionConstants.PayLaterProcessorKey,
                Status = PaymentStatus.Succeeded,
            };

            subscriptionPaymentMetadata.Subscriptions.Add(new SubscriptionInfo
            {
                SubscriptionId = subscriptionId,
                StartedAt = now,
                ExpiresAt = BillingSchedule.GetNextBillingDate(now, subscription.Key.Type, subscription.Key.Duration),
                GatewayMode = gatewayMode,
                Gateway = SubscriptionConstants.PayLaterProcessorKey,
            });
        }

        await subscriptionPaymentSession.SetAsync(sessionId, metadata);

        session.Put(subscriptionPaymentMetadata);

        await subscriptionSessionStore.SaveAsync(session);
    }

    private static JsonHttpResult<PayLaterErrorResponse> Error(string message, int errorCode, int statusCode)
    {
        // The checkout scripts always read the response body as JSON. Expose both the 'error' and
        // 'ErrorMessage' shapes used across the different payment views so the real reason is shown
        // to the user instead of a generic "Unexpected error".
        return TypedResults.Json(
            new PayLaterErrorResponse(message, message, errorCode),
            statusCode: statusCode);
    }

    private sealed record PayLaterErrorResponse(string error, string ErrorMessage, int ErrorCode);
}
