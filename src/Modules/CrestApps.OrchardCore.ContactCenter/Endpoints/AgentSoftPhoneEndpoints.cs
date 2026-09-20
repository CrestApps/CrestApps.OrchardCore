using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

public static class AgentSoftPhoneEndpoints
{
    public const string SyncQueuedVoiceWorkRouteName = "ContactCenterAgentSoftPhoneSyncQueuedVoiceWork";
    public const string CurrentIncomingOfferRouteName = "ContactCenterAgentSoftPhoneCurrentIncomingOffer";
    public const string SoftPhoneRegistrationConfigRouteName = "ContactCenterAgentSoftPhoneRegistrationConfig";
    public const string SoftPhoneSignOutRouteName = "ContactCenterAgentSoftPhoneSignOut";

    // The soft-phone clients (browser extension and Windows app) bind the call's State/Direction as
    // strings -- the same shape the Telephony hub sends. Serialize enums as their names here so the
    // current-offer recovery poll can parse the payload; the default HTTP options would emit numbers and
    // the client's string binding would fail (`$.call.state could not be converted to System.String`).
    private static readonly JsonSerializerOptions _incomingOfferJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Maps the agent soft phone endpoints.
    /// </summary>
    /// <remarks>
    /// What an out-of-page soft phone polls for: queued work, the current offer, its registration and sign-out.
    /// </remarks>
    /// <param name="builder">The route builder to map onto.</param>
    /// <param name="adminUrlPrefix">
    /// The prefix the host puts its administration routes behind. Defaults to "Admin", which is the
    /// prefix these routes have always used.
    /// </param>
    /// <param name="configure">
    /// Applied to every route mapped here, so a host can add its own filters or metadata.
    /// </param>
    /// <returns>The same route builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapContactCenterAgentSoftPhoneEndpoints(
        this IEndpointRouteBuilder builder,
        string adminUrlPrefix = null,
        Action<RouteHandlerBuilder> configure = null)
    {
        var routePrefix = string.IsNullOrWhiteSpace(adminUrlPrefix)
            ? "Admin"
            : adminUrlPrefix.Trim('/');

        var syncQueuedVoiceWork = builder.MapPost($"{routePrefix}/contact-center/agent/sync-queued-voice-work", HandleSyncQueuedVoiceWorkAsync)
            .WithName(SyncQueuedVoiceWorkRouteName);

        configure?.Invoke(syncQueuedVoiceWork);

        var currentIncomingOffer = builder.MapGet($"{routePrefix}/contact-center/agent/current-incoming-offer", HandleCurrentIncomingOfferAsync)
            .WithName(CurrentIncomingOfferRouteName);

        configure?.Invoke(currentIncomingOffer);

        var registrationConfig = builder.MapGet($"{routePrefix}/contact-center/agent/soft-phone/registration-config", HandleRegistrationConfigAsync)
            .WithName(SoftPhoneRegistrationConfigRouteName);

        configure?.Invoke(registrationConfig);

        var signOut = builder.MapPost($"{routePrefix}/contact-center/agent/soft-phone/sign-out", HandleSignOutAsync)
            .WithName(SoftPhoneSignOutRouteName);

        configure?.Invoke(signOut);

        return builder;
    }

    internal static async Task<IResult> HandleSyncQueuedVoiceWorkAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IQueuedVoiceWorkOfferService queuedVoiceWorkOfferService,
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

        await queuedVoiceWorkOfferService.OfferForUserAsync(userId, httpContext.RequestAborted);

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
            : TypedResults.Json(offer, _incomingOfferJsonOptions, statusCode: StatusCodes.Status200OK);
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

        if (provider is not ITelephonySoftPhoneCredentialsProvider credentialsProvider)
        {
            return TypedResults.NotFound();
        }

        var credentials = await credentialsProvider.GetClientCredentialsAsync(httpContext.RequestAborted);

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
