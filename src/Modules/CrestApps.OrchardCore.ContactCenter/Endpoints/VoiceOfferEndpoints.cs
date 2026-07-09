using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class VoiceOfferEndpoints
{
    public const string AcceptOfferRouteName = "ContactCenterVoiceAcceptOffer";
    public const string DeclineOfferRouteName = "ContactCenterVoiceDeclineOffer";

    public static IEndpointRouteBuilder AddVoiceOfferEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("Admin/contact-center/voice/offer/accept", HandleAcceptAsync)
            .WithName(AcceptOfferRouteName);

        builder.MapPost("Admin/contact-center/voice/offer/decline", HandleDeclineAsync)
            .WithName(DeclineOfferRouteName);

        return builder;
    }

    private static async Task<IResult> HandleAcceptAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IContactCenterCallCommandService callCommandService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var reservationId = await ResolveReservationIdAsync(httpContext);

        if (string.IsNullOrEmpty(reservationId))
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var result = await callCommandService.AcceptInboundOfferAsync(reservationId, userId, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.RequiresDeviceAnswer,
            result.InteractionId,
            result.CallSessionId,
        });
    }

    private static async Task<IResult> HandleDeclineAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IContactCenterCallCommandService callCommandService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var reservationId = await ResolveReservationIdAsync(httpContext);

        if (string.IsNullOrEmpty(reservationId))
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var result = await callCommandService.DeclineInboundOfferAsync(reservationId, userId, httpContext.RequestAborted);

        return result.Succeeded
            ? TypedResults.Ok()
            : TypedResults.NotFound();
    }

    private sealed class ReservationRequest
    {
        public string ReservationId { get; set; }
    }

    private static async Task<string> ResolveReservationIdAsync(HttpContext httpContext)
    {
        var reservationId = httpContext.Request.Query["reservationId"].ToString();

        if (!string.IsNullOrWhiteSpace(reservationId))
        {
            return reservationId;
        }

        if (httpContext.Request.HasFormContentType)
        {
            var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
            reservationId = form["reservationId"].ToString();

            if (!string.IsNullOrWhiteSpace(reservationId))
            {
                return reservationId;
            }
        }

        if (httpContext.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ReservationRequest>(cancellationToken: httpContext.RequestAborted);

            if (!string.IsNullOrWhiteSpace(request?.ReservationId))
            {
                return request.ReservationId;
            }
        }

        return null;
    }
}
