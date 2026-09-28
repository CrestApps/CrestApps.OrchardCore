using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class SecureCaptureEndpoints
{
    public const string BeginRouteName = "ContactCenterSecureCaptureBegin";
    public const string CancelRouteName = "ContactCenterSecureCaptureCancel";

    public static IEndpointRouteBuilder AddSecureCaptureEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("Admin/contact-center/workspace/secure-capture/begin", HandleBeginAsync)
            .WithName(BeginRouteName);

        builder.MapPost("Admin/contact-center/workspace/secure-capture/cancel", HandleCancelAsync)
            .WithName(CancelRouteName);

        return builder;
    }

    private static async Task<IResult> HandleBeginAsync(
        [FromForm] SecureCaptureBeginRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        ISecureCaptureService secureCaptureService,
        LinkGenerator linkGenerator,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InitiateSecureCapture))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TypedResults.BadRequest();
        }

        var fields = ParseFields(request.Fields);

        if (fields.Count == 0)
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await secureCaptureService.BeginAsync(
            request.InteractionId,
            userId,
            httpContext.User,
            fields,
            httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return TypedResults.Ok(new
            {
                result.Succeeded,
                result.Reason,
            });
        }

        var captureUrl = linkGenerator.GetUriByRouteValues(
            httpContext,
            SecureCaptureController.CaptureRouteName,
            new { token = result.AccessToken });

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.SessionId,
            result.ExpiresUtc,
            CaptureUrl = captureUrl,
        });
    }

    private static async Task<IResult> HandleCancelAsync(
        [FromForm] SecureCaptureCancelRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        ISecureCaptureService secureCaptureService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InitiateSecureCapture))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.SessionId))
        {
            return TypedResults.BadRequest();
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await secureCaptureService.CancelAsync(
            request.SessionId,
            userId,
            httpContext.User,
            httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.Reason,
        });
    }

    private static List<SecureCaptureField> ParseFields(string fields)
    {
        var parsed = new List<SecureCaptureField>();

        if (string.IsNullOrEmpty(fields))
        {
            return parsed;
        }

        foreach (var token in fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<SecureCaptureField>(token, ignoreCase: true, out var field)
                && Enum.IsDefined(field)
                && !parsed.Contains(field))
            {
                parsed.Add(field);
            }
        }

        return parsed;
    }

    private sealed class SecureCaptureBeginRequest
    {
        public string InteractionId { get; set; }

        public string Fields { get; set; }
    }

    private sealed class SecureCaptureCancelRequest
    {
        public string SessionId { get; set; }
    }
}
