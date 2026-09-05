using System.Security.Claims;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Telephony.Endpoints;

/// <summary>
/// Endpoint that lets an operator start an outbound call from outside the soft phone surface (the "call" button
/// next to a phone-number field). It does not place the call itself: it pushes a <see cref="TelephonyDialRequest"/>
/// to the caller's own soft phone through the telephony hub, so the call is placed wherever the soft phone is
/// connected (the in-page widget or the browser-extension window). The soft phone decides how to place it,
/// including registering first when needed or holding an active call. Presence is never changed here.
/// </summary>
internal static class SoftPhoneDialerEndpoints
{
    public static IEndpointRouteBuilder AddSoftPhoneDialerEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("softphone/dial", HandleDialAsync)
            .WithName("TelephonySoftPhoneDial");

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
