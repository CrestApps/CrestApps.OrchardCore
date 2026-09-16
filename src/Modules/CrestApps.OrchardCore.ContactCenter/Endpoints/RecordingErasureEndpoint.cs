using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class RecordingErasureEndpoint
{
    public const string RouteName = "ContactCenterRecordingErasure";

    public static IEndpointRouteBuilder AddRecordingErasureEndpoint(
        this IEndpointRouteBuilder builder,
        string adminUrlPrefix)
    {
        var routePrefix = string.IsNullOrWhiteSpace(adminUrlPrefix)
            ? "Admin"
            : adminUrlPrefix.Trim('/');

        builder.MapPost($"{routePrefix}/contact-center/recordings/erase", HandleAsync)
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        RecordingErasureRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IRecordingAccessGovernanceService recordingAccessGovernanceService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.ManageInteractions))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.InteractionId) ||
            string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest();
        }

        var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(actorId))
        {
            return TypedResults.Forbid();
        }

        // Erasure performs durable writes (pointer clears, tombstone, outbox media-deletion enqueue) that must not be
        // torn by a caller who disconnects, so the operation is not bound to the request abort token.
        var decision = await recordingAccessGovernanceService.EraseAsync(
            request.InteractionId,
            actorId,
            request.Reason,
            CancellationToken.None);

        return TypedResults.Ok(decision);
    }
}
