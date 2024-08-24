using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
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
        [FromBody] PayLaterRequest model,
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

        var session = await subscriptionSessionStore.GetAsync(model.SessionId);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var invoice = session.As<Invoice>();

        if (invoice == null)
        {
            return TypedResults.NotFound();
        }

        await subscriptionPaymentSession.SetAsync(model.SessionId, new InitialPaymentMetadata()
        {
            Amount = invoice.InitialPaymentAmount ?? 0,
            Currency = invoice.Currency,
            Mode = Payments.GatewayMode.Production,
        });

        await subscriptionPaymentSession.SetAsync(model.SessionId, new SubscriptionPaymentMetadata()
        {
            Amount = invoice.FirstSubscriptionPaymentAmount ?? 0,
            Currency = invoice.Currency,
            Mode = Payments.GatewayMode.Production,
        });

        return TypedResults.Ok(new
        {
            status = "completed",
        });
    }
}
