using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

public static class RecordingErasureEndpoint
{
    public const string RouteName = "ContactCenterRecordingErasure";

    /// <summary>
    /// Maps the recording erasure endpoint.
    /// </summary>
    /// <remarks>
    /// Erasing a recording, which is not undoable.
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
    public static IEndpointRouteBuilder MapContactCenterRecordingErasureEndpoint(
        this IEndpointRouteBuilder builder,
        string adminUrlPrefix = null,
        Action<RouteHandlerBuilder> configure = null)
    {
        var routePrefix = string.IsNullOrWhiteSpace(adminUrlPrefix)
            ? "Admin"
            : adminUrlPrefix.Trim('/');

        var route = builder.MapPost($"{routePrefix}/contact-center/recordings/erase", HandleAsync)
            .WithName(RouteName);

        configure?.Invoke(route);

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
