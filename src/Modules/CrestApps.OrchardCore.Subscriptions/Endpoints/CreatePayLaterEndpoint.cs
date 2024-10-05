using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
        SubscriptionPaymentSession subscriptionPaymentSession)
    {
        if (string.IsNullOrEmpty(model?.SessionId))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
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

        var now = clock.UtcNow;

        var collection = new SubscriptionCollectionMetadata()
        {
            Subscriptions = [],
        };

        // Here we have to group subscriptions per duration to determine the proper expiration date.
        collection.Subscriptions.Add(new SubscriptionMetadata
        {
            Gateway = SubscriptionConstants.PayLaterProcessorKey,
            GatewayMode = Payments.GatewayMode.Live,
            StartedAt = now,
            ExpiresAt = null,
        });

        session.Put(collection);

        await subscriptionPaymentSession.SetAsync(model.SessionId, new InitialPaymentMetadata()
        {
            Amount = invoice.InitialPaymentAmount ?? 0,
            Currency = invoice.Currency,
            Mode = Payments.GatewayMode.Live,
        });

        var metadata = new SubscriptionPaymentsMetadata()
        {
            Payments = new Dictionary<string, SubscriptionPaymentMetadata>(),
        };

        // Group line items by subscription duration, ensuring that each subscription has a single, unified expiration date.
        var subscriptionGroups = invoice.GetSubscriptionGroups();

        var subscriptionPaymentMetadata = new SubscriptionCollectionMetadata()
        {
            Subscriptions = [],
        };

        foreach (var subscription in subscriptionGroups)
        {
            var subscriptionId = IdGenerator.GenerateId();

            metadata.Payments[subscriptionId] = new SubscriptionPaymentMetadata()
            {
                SubscriptionId = subscriptionId,
                Currency = invoice.Currency,
                Amount = subscription.Value.Sum(x => x.GetLineTotal()),
                GatewayMode = Payments.GatewayMode.Live,
                GatewayId = SubscriptionConstants.PayLaterProcessorKey,
            };

            subscriptionPaymentMetadata.Subscriptions.Add(new SubscriptionMetadata
            {
                SubscriptionId = subscriptionId,
                StartedAt = now,
                ExpiresAt = subscription.Key.Type switch
                {
                    DurationType.Day => now.AddDays(subscription.Key.Duration),
                    DurationType.Week => now.AddDays(subscription.Key.Duration * 7),
                    DurationType.Month => now.AddMonths(subscription.Key.Duration),
                    DurationType.Year => now.AddYears(subscription.Key.Duration),
                    _ => null
                },
                GatewayMode = Payments.GatewayMode.Live,
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
