using System.Security.Claims;
using CrestApps.OrchardCore.Telephony.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Telephony.Endpoints;

/// <summary>
/// Endpoints consumed by the CrestApps Soft Phone browser extension. The extension calls the configuration
/// endpoint once after the user is authenticated to discover the tenant-aware hub and offer URLs, then opens
/// its own background connection to receive inbound calls even while the phone window is closed.
/// </summary>
internal static class SoftPhoneExtensionEndpoints
{
    /// <summary>
    /// The well-known route name of the Contact Center pending-incoming-offer endpoint. It is referenced by
    /// name (not by a type reference) so the Telephony module stays decoupled from Contact Center: when
    /// Contact Center is not enabled the route does not resolve and the URL is simply omitted.
    /// </summary>
    private const string CurrentIncomingOfferRouteName = "ContactCenterAgentSoftPhoneCurrentIncomingOffer";

    public static IEndpointRouteBuilder AddSoftPhoneExtensionEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("softphone/extension-config", HandleExtensionConfigAsync)
            .WithName("TelephonySoftPhoneExtensionConfig");

        return builder;
    }

    internal static async Task<IResult> HandleExtensionConfigAsync(
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, TelephonyPermissions.UseSoftPhone))
        {
            return TypedResults.Forbid();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        var request = httpContext.Request;
        var origin = $"{request.Scheme}://{request.Host}{request.PathBase}";

        // The offer endpoint lives in Contact Center; resolve it by name so it is present only when that
        // module is enabled. When absent, the extension still works for outbound and browser-originated
        // calls; queued inbound offers simply light up once Contact Center is enabled.
        var currentIncomingOfferUrl = linkGenerator.GetUriByName(
            httpContext,
            CurrentIncomingOfferRouteName,
            values: null);

        var displayName = string.IsNullOrWhiteSpace(httpContext.User.Identity?.Name)
            ? userId
            : httpContext.User.Identity.Name;

        var config = new SoftPhoneExtensionConfig
        {
            HubUrl = origin + SignalRHubRoutes.GetHubPath<TelephonyHub>(),
            SoftPhoneUrl = origin + "/softphone",
            CurrentIncomingOfferUrl = currentIncomingOfferUrl,
            UserId = userId,
            DisplayName = displayName,
        };

        return TypedResults.Ok(config);
    }

    private sealed class SoftPhoneExtensionConfig
    {
        public string HubUrl { get; init; }

        public string SoftPhoneUrl { get; init; }

        public string CurrentIncomingOfferUrl { get; init; }

        public string UserId { get; init; }

        public string DisplayName { get; init; }
    }
}
