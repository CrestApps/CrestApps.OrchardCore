using System.Security.Claims;
using CrestApps.Core.SignalR;
using CrestApps.OrchardCore.Telephony.Hubs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Endpoints;

/// <summary>
/// Endpoint that lets an operator start an outbound call from outside the soft phone surface (the "call" button
/// next to a phone-number field). It does not place the call itself: it pushes a <see cref="TelephonyDialRequest"/>
/// to the caller's own soft phone through the telephony hub, so the call is placed wherever the soft phone is
/// connected (the in-page widget or the browser-extension window). The soft phone decides how to place it,
/// including registering first when needed or holding an active call. Presence is never changed here.
/// </summary>
public static class SoftPhoneDialerEndpoints
{
    /// <summary>
    /// Maps the soft phone dial endpoint.
    /// </summary>
    /// <remarks>
    /// The soft phone dial endpoint.
    /// </remarks>
    /// <param name="builder">The route builder to map onto.</param>
    /// <param name="configure">
    /// Applied to every route mapped here, so a host can add its own filters or metadata.
    /// </param>
    /// <returns>The same route builder, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapSoftPhoneDialerEndpoints(
        this IEndpointRouteBuilder builder,
        Action<RouteHandlerBuilder> configure = null)
    {
        var dial = builder.MapPost("softphone/dial", HandleDialAsync)
            .WithName("TelephonySoftPhoneDial");

        configure?.Invoke(dial);

        return builder;
    }

    internal static async Task<IResult> HandleDialAsync(
        [FromForm] string number,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        ShellSettings shellSettings,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, TelephonyPermissions.UseSoftPhone))
        {
            return TypedResults.Forbid();
        }

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Forbid();
        }

        number = number?.Trim();

        if (string.IsNullOrEmpty(number))
        {
            return TypedResults.BadRequest();
        }

        // Push to the caller's own soft phone group. When no soft phone is connected the push simply reaches no
        // client; the button surfaces that to the operator with its own transient state.
        await hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(shellSettings.Name, userId))
            .DialRequested(new TelephonyDialRequest { Number = number });

        return TypedResults.Accepted((string)null);
    }
}
