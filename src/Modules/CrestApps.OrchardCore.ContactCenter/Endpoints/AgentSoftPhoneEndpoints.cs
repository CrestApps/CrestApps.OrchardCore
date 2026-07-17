using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
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
    public const string SoftPhoneRegistrationConfigRouteName = "ContactCenterAgentSoftPhoneRegistrationConfig";
    public const string SoftPhoneSignOutRouteName = "ContactCenterAgentSoftPhoneSignOut";

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

        builder.MapGet($"{routePrefix}/contact-center/agent/soft-phone/registration-config", HandleRegistrationConfigAsync)
            .WithName(SoftPhoneRegistrationConfigRouteName);

        builder.MapPost($"{routePrefix}/contact-center/agent/soft-phone/sign-out", HandleSignOutAsync)
            .WithName(SoftPhoneSignOutRouteName);

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

    internal static async Task<IResult> HandleRegistrationConfigAsync(
        IAuthorizationService authorizationService,
        ITelephonyProviderResolver telephonyProviderResolver,
        IEnumerable<ISoftPhoneRegistrationConfigContributor> registrationConfigContributors,
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

        var provider = await telephonyProviderResolver.GetAsync();

        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var credentials = await provider.GetClientCredentialsAsync(httpContext.RequestAborted);

        if (credentials?.AudioMode != TelephonyAudioMode.Browser || string.IsNullOrWhiteSpace(credentials.BrowserMediaAdapterName))
        {
            return TypedResults.NotFound();
        }

        var contributor = registrationConfigContributors.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderName, credentials.ProviderName, StringComparison.Ordinal)) ??
            registrationConfigContributors.FirstOrDefault();

        if (contributor is null)
        {
            return TypedResults.NotFound();
        }

        var displayName = string.IsNullOrWhiteSpace(httpContext.User.Identity?.Name)
            ? userId
            : httpContext.User.Identity.Name;

        // The interaction id is caller-controlled and is therefore treated as non-authoritative metadata
        // only. It never authorizes issuance and never becomes the media session identity; the provider
        // derives ownership from the authenticated user and generates a server-owned session id.
        var interactionId = httpContext.Request.Query.TryGetValue("interactionId", out var interactionValues)
            ? interactionValues.FirstOrDefault()
            : null;
        var config = await contributor.BuildAsync(new SoftPhoneRegistrationConfigContext
        {
            ProviderName = credentials.ProviderName,
            UserId = userId,
            DisplayName = displayName,
            InteractionId = string.IsNullOrWhiteSpace(interactionId)
                ? null
                : interactionId,
        }, httpContext.RequestAborted);

        return config is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(config);
    }

    internal static async Task<IResult> HandleSignOutAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<ISoftPhoneCredentialRevoker> credentialRevokers,
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

        foreach (var revoker in credentialRevokers)
        {
            await revoker.RevokeForUserAsync(userId, "signed_out", httpContext.RequestAborted);
        }

        return TypedResults.Ok();
    }
}
