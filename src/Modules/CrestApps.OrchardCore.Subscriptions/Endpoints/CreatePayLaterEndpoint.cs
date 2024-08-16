using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

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
        [FromBody] string sessionId,
        ISubscriptionSessionStore subscriptionSessionStore,
        SubscriptionPaymentSession subscriptionPaymentSession)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        var session = await subscriptionSessionStore.GetAsync(sessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var invoice = session.As<Invoice>();

        if (invoice == null)
        {
            return TypedResults.NotFound();
        }

        await subscriptionPaymentSession.SetAsync(sessionId, new InitialPaymentMetadata()
        {
            InitialPaymentAmount = invoice.DueNow,
        });

        await subscriptionPaymentSession.SetAsync(sessionId, new SubscriptionPaymentMetadata()
        {
            PlanId = session.ContentItemVersionId,
        });

        return TypedResults.Ok(new
        {
            status = "completed",
        });
    }
}
