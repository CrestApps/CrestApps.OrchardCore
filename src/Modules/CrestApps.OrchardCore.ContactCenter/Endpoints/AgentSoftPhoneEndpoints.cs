using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class AgentSoftPhoneEndpoints
{
    public const string SyncQueuedVoiceWorkRouteName = "ContactCenterAgentSoftPhoneSyncQueuedVoiceWork";
    public const string CurrentIncomingOfferRouteName = "ContactCenterAgentSoftPhoneCurrentIncomingOffer";

    public static IEndpointRouteBuilder AddAgentSoftPhoneEndpoints(
        this IEndpointRouteBuilder builder,
        string adminUrlPrefix)
    {
        var routePrefix = string.IsNullOrWhiteSpace(adminUrlPrefix)
            ? "Admin"
            : adminUrlPrefix.Trim('/');

        builder.MapPost($"{routePrefix}/contact-center/agent/sync-queued-voice-work", HandleSyncQueuedVoiceWorkAsync)
            .WithName(SyncQueuedVoiceWorkRouteName);

        builder.MapGet($"{routePrefix}/contact-center/agent/current-incoming-offer", HandleCurrentIncomingOfferAsync)
            .WithName(CurrentIncomingOfferRouteName);

        return builder;
    }

    internal static async Task<IResult> HandleSyncQueuedVoiceWorkAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IQueuedVoiceWorkOfferService> queuedVoiceWorkOfferServices,
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

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var queuedVoiceWorkOfferService = queuedVoiceWorkOfferServices.FirstOrDefault();

        if (queuedVoiceWorkOfferService is not null)
        {
            await queuedVoiceWorkOfferService.OfferForUserAsync(userId, httpContext.RequestAborted);
        }

        return TypedResults.Ok();
    }

    internal static async Task<IResult> HandleCurrentIncomingOfferAsync(
        IAuthorizationService authorizationService,
        IEnumerable<IPendingIncomingCallOfferService> pendingIncomingCallOfferServices,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return TypedResults.Forbid();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var pendingIncomingCallOfferService = pendingIncomingCallOfferServices.FirstOrDefault();

        if (pendingIncomingCallOfferService is null)
        {
            return TypedResults.NotFound();
        }

        var offer = await pendingIncomingCallOfferService.GetForUserAsync(userId, httpContext.RequestAborted);

        return offer is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(offer);
    }
}
