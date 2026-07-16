using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class VoiceIngressEndpoint
{
    public static IEndpointRouteBuilder AddVoiceIngressEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/contact-center/voice/inbound", HandleAsync)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        InboundVoiceEvent inboundEvent,
        IAuthorizationService authorizationService,
        IVoiceContactCenterCallRouter voiceCallRouter,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.ManageInteractions))
        {
            return TypedResults.Forbid();
        }

        if (inboundEvent is null)
        {
            return TypedResults.BadRequest();
        }

        var result = await voiceCallRouter.RouteInboundAsync(inboundEvent, httpContext.RequestAborted);

        return TypedResults.Ok(result);
    }
}
