using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

public static class CreatePayLaterEndpoint
{
    public static IEndpointRouteBuilder AddCreatePayLaterEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/pay-later/process", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreatePayLaterEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] PayLaterRequest model,
        IClock clock,
        ISubscriptionSessionStore subscriptionSessionStore,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IPaymentAttemptLimiter paymentAttemptLimiter,
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment hostEnvironment)
    {
        if (string.IsNullOrEmpty(model?.SessionId))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContextAccessor.HttpContext, "pay-later", model.SessionId))
        {
            return PaymentEndpointThrottle.TooManyRequests();
        }

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        if (!session.TryGet<Invoice>(out var invoice))
        {
            return TypedResults.NotFound();
        }

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

        await subscriptionPaymentSession.SetAsync(model.SessionId, new InitialPaymentMetadata()
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

        await subscriptionPaymentSession.SetAsync(model.SessionId, metadata);

        session.Put(subscriptionPaymentMetadata);

        await subscriptionSessionStore.SaveAsync(session);

        return TypedResults.Ok(new
        {
            status = "completed",
        });
    }
}
